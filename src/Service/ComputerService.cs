using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Computer Service
    /// </summary>
    public class ComputerService : IComputerService
    {
        #region Fields

        private Memory _memory = new Memory(new Structs.Windows.MemoryStatusEx());
        private OperatingSystem _operatingSystem;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the memory info (RAM)
        /// </summary>
        public Memory Memory
        {
            get
            {
                try
                {
                    var memoryStatusEx = new Structs.Windows.MemoryStatusEx();

                    if (!NativeMethods.GlobalMemoryStatusEx(memoryStatusEx))
                        Logger.Error(new Win32Exception(Marshal.GetLastWin32Error()));

                    _memory = new Memory(memoryStatusEx);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

                return _memory;
            }
        }

        /// <summary>
        /// Gets the operating system info
        /// </summary>
        public OperatingSystem OperatingSystem
        {
            get
            {
                if (_operatingSystem is null)
                {
                    var operatingSystem = Environment.OSVersion;

                    _operatingSystem = new OperatingSystem
                    {
                        Is64Bit = Environment.Is64BitOperatingSystem,
                        IsWindows7OrGreater = (operatingSystem.Version.Major > 6) || (operatingSystem.Version.Major == 6 && operatingSystem.Version.Minor >= 1),
                        IsWindows8OrGreater = operatingSystem.Version >= new Version(6, 2),
                        IsWindows81OrGreater = operatingSystem.Version >= new Version(6, 3),
                        IsWindowsVistaOrGreater = operatingSystem.Version.Major >= 6,
                        IsWindowsXpOrGreater = operatingSystem.Version >= new Version(5, 1)
                    };
                }

                return _operatingSystem;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when [optimize progress is update].
        /// </summary>
        public event Action<byte, string> OnOptimizeProgressUpdate;

        #endregion

        #region Methods (Computer)

        private static SafeFileHandle OpenVolumeHandle(string driveLetter)
        {
            if (string.IsNullOrWhiteSpace(driveLetter))
                return null;

            return NativeMethods.CreateFile
            (
                @"\\.\" + driveLetter.TrimEnd(':', '\\') + ":",
                FileAccess.ReadWrite,
                FileShare.Read | FileShare.Write,
                IntPtr.Zero,
                FileMode.Open,
                (int)FileAttributes.Normal | Constants.Windows.File.FlagsNoBuffering,
                IntPtr.Zero
            );
        }

        /// <summary>
        /// Increase the Privilege using a privilege name
        /// </summary>
        /// <param name="privilegeName">The name of the privilege that needs to be increased</param>
        /// <returns></returns>
        private bool SetIncreasePrivilege(string privilegeName)
        {
            var result = false;

            using (var current = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges))
            {
                Structs.Windows.TokenPrivileges newState;
                newState.Count = 1;
                newState.Luid = 0L;
                newState.Attr = Constants.Windows.PrivilegeAttribute.Enabled;

                // Retrieves the uid used on a specified system to locally represent the specified privilege name
                if (NativeMethods.LookupPrivilegeValue(null, privilegeName, ref newState.Luid))
                {
                    // Enables or disables privileges in a specified access token
                    // AdjustTokenPrivileges retorna TRUE mesmo com ERROR_NOT_ALL_ASSIGNED (1300): o privilégio não foi concedido
                    result = NativeMethods.AdjustTokenPrivileges(current.Token, false, ref newState, 0, IntPtr.Zero, IntPtr.Zero) && Marshal.GetLastWin32Error() != 1300;
                }

                if (!result)
                    Logger.Error(new Win32Exception(Marshal.GetLastWin32Error()));
            }

            return result;
        }

        #endregion

        #region Methods (Memory)

        /// <summary>
        /// Optimize the computer
        /// </summary>
        /// <param name="reason">Optimization reason</param>
        /// <param name="areas">Memory areas</param>
        /// <returns>Resultado por área; vazio quando nada foi executado</returns>
        public OptimizationResult Optimize(Enums.Memory.Optimization.Reason reason, Enums.Memory.Areas areas)
        {
            if (areas == Enums.Memory.Areas.None)
                return OptimizationResult.Skipped;

            // Exclusão mútua entre processos (GUI, serviço e linha de comando): se outra otimização está em curso, esta é descartada
            System.Threading.Mutex mutex = null;
            var acquired = true;

            try
            {
                mutex = new System.Threading.Mutex(false, @"Global\" + Constants.App.Id + ".Optimize");

                try
                {
                    acquired = mutex.WaitOne(0);
                }
                catch (System.Threading.AbandonedMutexException)
                {
                    // o dono anterior terminou sem liberar; o mutex agora pertence a este processo
                    acquired = true;
                }
            }
            catch (Exception e)
            {
                // sem exclusão mútua disponível (ex.: acesso negado): segue sem ela
                Logger.Debug(e.Message);
                mutex?.Dispose();
                mutex = null;
            }

            if (!acquired)
            {
                Logger.Information(Localizer.String.Optimize + ": another optimization is already running, skipped");
                mutex.Dispose();

                return OptimizationResult.Skipped;
            }

            try
            {
                var freeBefore = Memory.Physical.Free.Bytes;
                var result = OptimizeAreas(reason, areas);

                OptimizationHistory.Append(OptimizationHistory.FromResult(result, reason.GetString(), Memory.Physical.Free.Bytes - freeBefore, DateTimeOffset.Now));

                return result;
            }
            finally
            {
                if (mutex is not null)
                {
                    mutex.ReleaseMutex();
                    mutex.Dispose();
                }
            }
        }

        /// <summary>
        /// Executa a otimização de cada área, isolando falhas por área
        /// </summary>
        private OptimizationResult OptimizeAreas(Enums.Memory.Optimization.Reason reason, Enums.Memory.Areas areas)
        {
            var result = new OptimizationResult();
            var optimizationReason = reason.GetString();
            var value = (byte)0;

            // Com as duas áreas de standby marcadas, o purge completo prevalece sobre o de baixa prioridade
            var lowPriority = (areas & Enums.Memory.Areas.StandbyListLowPriority) != 0 && (areas & Enums.Memory.Areas.StandbyList) == 0;

            var steps = new List<(Enums.Memory.Areas Flags, string Name, Action Run)>
            {
                (Enums.Memory.Areas.WorkingSet, Localizer.String.WorkingSet, OptimizeWorkingSet),
                (Enums.Memory.Areas.SystemFileCache, Localizer.String.SystemFileCache, OptimizeSystemFileCache),
                (Enums.Memory.Areas.ModifiedPageList, Localizer.String.ModifiedPageList, OptimizeModifiedPageList),
                (Enums.Memory.Areas.StandbyList | Enums.Memory.Areas.StandbyListLowPriority, lowPriority ? Localizer.String.StandbyListLowPriority : Localizer.String.StandbyList, () => OptimizeStandbyList(lowPriority)),
                (Enums.Memory.Areas.CombinedPageList, Localizer.String.CombinedPageList, OptimizeCombinedPageList),
                (Enums.Memory.Areas.RegistryCache, Localizer.String.RegistryCache, OptimizeRegistryCache),
                (Enums.Memory.Areas.ModifiedFileCache, Localizer.String.ModifiedFileCache, OptimizeModifiedFileCache)
            };

            var stopwatch = new Stopwatch();

            foreach (var step in steps)
            {
                if ((areas & step.Flags) == 0)
                    continue;

                var areaResult = new OptimizationAreaResult { Name = step.Name };

                try
                {
                    if (OnOptimizeProgressUpdate is not null)
                    {
                        value++;
                        OnOptimizeProgressUpdate(value, step.Name);
                    }

                    stopwatch.Restart();

                    step.Run();
                }
                catch (Exception e)
                {
                    areaResult.Error = e.GetMessage();
                }

                areaResult.Elapsed = stopwatch.Elapsed;
                result.Areas.Add(areaResult);
            }

            // Garbage Collector
            try
            {
                if (OnOptimizeProgressUpdate is not null)
                {
                    value++;
                    OnOptimizeProgressUpdate(value, Localizer.String.GarbageCollector);
                }

                App.ReleaseMemory();
            }
            catch
            {
                // ignored
            }

            // Log
            try
            {
                LogResult(optimizationReason, result.Areas.Where(area => area.Succeeded), Enums.Log.Levels.Information, Localizer.String.MemoryOptimized);
                LogResult(optimizationReason, result.Failed, Enums.Log.Levels.Error, Localizer.String.Invalid);
            }
            catch
            {
                // ignored
            }

            return result;
        }

        /// <summary>
        /// Registra no log as áreas informadas com a duração total
        /// </summary>
        private static void LogResult(string reason, IEnumerable<OptimizationAreaResult> areaResults, Enums.Log.Levels level, string message)
        {
            var list = areaResults.ToList();

            if (list.Count == 0)
                return;

            var data = new LogOptimizationData { Reason = reason };

            foreach (var area in list)
            {
                data.MemoryAreas.Add(new LogOptimizationDataMemoryArea
                {
                    Name = area.Name,
                    Duration = string.Format(Localizer.Culture, "{0:0.0} {1}", area.Elapsed.TotalSeconds, Localizer.String.Seconds.ToLower(Localizer.Culture)),
                    Error = area.Error
                });
            }

            data.Duration = string.Format(Localizer.Culture, "{0:0.0} {1}", list.Aggregate(TimeSpan.Zero, (total, area) => total + area.Elapsed).TotalSeconds, Localizer.String.Seconds.ToLower(Localizer.Culture));

            Logger.Log(new Log(level, message, data));
        }

        /// <summary>
        /// Optimize the combined page list.
        /// </summary>
        private void OptimizeCombinedPageList()
        {
            // Windows minimum version
            if (!OperatingSystem.HasCombinedPageList)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.CombinedPageList));

            // Check privilege
            if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName))
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorAdminPrivilegeRequired, Constants.Windows.Privilege.SeProfSingleProcessName));

            var handle = GCHandle.Alloc(0);

            try
            {
                var memoryCombineInformationEx = new Structs.Windows.MemoryCombineInformationEx();

                handle = GCHandle.Alloc(memoryCombineInformationEx, GCHandleType.Pinned);

                if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemCombinePhysicalMemoryInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(memoryCombineInformationEx)) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                try
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
                catch (InvalidOperationException)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Optimize the modified file cache.
        /// </summary>
        private void OptimizeModifiedFileCache()
        {
            // Windows minimum version
            if (!OperatingSystem.HasModifiedFileCache)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.ModifiedFileCache));

            // Falha em um volume não impede os demais; os erros são agregados e lançados ao final
            var volumeErrors = new StringBuilder();

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive is null || drive.DriveType != DriveType.Fixed || string.IsNullOrWhiteSpace(drive.Name))
                    continue;

                using (var handle = OpenVolumeHandle(drive.Name))
                {
                    if (handle is null || handle.IsInvalid)
                    {
                        if (volumeErrors.Length > 0)
                            volumeErrors.Append(" | ");

                        volumeErrors.Append(drive.Name).Append(": ").Append(new Win32Exception(Marshal.GetLastWin32Error()).Message);

                        continue;
                    }

                    int bytesReturned;

                    if (OperatingSystem.IsWindows7OrGreater)
                    {
                        try
                        {
                            var buffer = Marshal.AllocHGlobal(1);

                            try
                            {
                                if (!NativeMethods.DeviceIoControl(
                                    handle,
                                    Constants.Windows.Drive.IoControlResetWriteOrder,
                                    buffer,
                                    1,
                                    IntPtr.Zero,
                                    0,
                                    out bytesReturned,
                                    IntPtr.Zero))
                                {
                                    throw new Win32Exception(Marshal.GetLastWin32Error());
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(buffer);
                            }
                        }
                        catch
                        {
                            // ignored
                        }

                        if (OperatingSystem.IsWindows8OrGreater)
                        {
                            try
                            {
                                if (!NativeMethods.DeviceIoControl(
                                    handle,
                                    Constants.Windows.Drive.FsctlDiscardVolumeCache,
                                    IntPtr.Zero,
                                    0,
                                    IntPtr.Zero,
                                    0,
                                    out bytesReturned,
                                    IntPtr.Zero))
                                {
                                    throw new Win32Exception(Marshal.GetLastWin32Error());
                                }
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }

                    if (!NativeMethods.FlushFileBuffers(handle))
                    {
                        if (volumeErrors.Length > 0)
                            volumeErrors.Append(" | ");

                        volumeErrors.Append(drive.Name).Append(": ").Append(new Win32Exception(Marshal.GetLastWin32Error()).Message);
                    }
                }
            }

            if (volumeErrors.Length > 0)
                throw new Exception(volumeErrors.ToString());
        }

        /// <summary>
        /// Optimize the modified page list.
        /// </summary>
        private void OptimizeModifiedPageList()
        {
            // Windows minimum version
            if (!OperatingSystem.HasModifiedPageList)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.ModifiedPageList));

            // Check privilege
            if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName))
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorAdminPrivilegeRequired, Constants.Windows.Privilege.SeProfSingleProcessName));

            var handle = GCHandle.Alloc(Constants.Windows.SystemMemoryListCommand.MemoryFlushModifiedList, GCHandleType.Pinned);

            try
            {
                if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemMemoryListInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(Constants.Windows.SystemMemoryListCommand.MemoryFlushModifiedList)) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                try
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
                catch (InvalidOperationException)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Optimize the registry cache.
        /// </summary>
        private void OptimizeRegistryCache()
        {
            // Windows minimum version
            if (!OperatingSystem.HasRegistryHive)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.RegistryCache));

            // Best-effort: o privilégio pode já estar habilitado por outra área; o erro real vem do NTSTATUS abaixo
            SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName);

            if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemRegistryReconciliationInformation, IntPtr.Zero, 0) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        /// <summary>
        /// Optimize the standby list.
        /// </summary>
        /// <param name="lowPriority">if set to <c>true</c> [low priority].</param>
        private void OptimizeStandbyList(bool lowPriority = false)
        {
            // Windows minimum version
            if (!OperatingSystem.HasStandbyList)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.StandbyList));

            // Check privilege
            if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName))
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorAdminPrivilegeRequired, Constants.Windows.Privilege.SeProfSingleProcessName));

            object memoryPurgeStandbyList = lowPriority ? Constants.Windows.SystemMemoryListCommand.MemoryPurgeLowPriorityStandbyList : Constants.Windows.SystemMemoryListCommand.MemoryPurgeStandbyList;
            var handle = GCHandle.Alloc(memoryPurgeStandbyList, GCHandleType.Pinned);

            try
            {
                if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemMemoryListInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(memoryPurgeStandbyList)) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                try
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
                catch (InvalidOperationException)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Optimize the system file cache.
        /// </summary>
        private void OptimizeSystemFileCache()
        {
            // Windows minimum version
            if (!OperatingSystem.HasSystemFileCache)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.SystemFileCache));

            // Check privilege
            if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeIncreaseQuotaName))
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorAdminPrivilegeRequired, Constants.Windows.Privilege.SeIncreaseQuotaName));

            var handle = GCHandle.Alloc(0);

            try
            {
                object systemFileCacheInformation;

                if (OperatingSystem.Is64Bit)
                    systemFileCacheInformation = new Structs.Windows.SystemFileCacheInformation64 { MinimumWorkingSet = -1L, MaximumWorkingSet = -1L };
                else
                    systemFileCacheInformation = new Structs.Windows.SystemFileCacheInformation32 { MinimumWorkingSet = int.MaxValue, MaximumWorkingSet = int.MaxValue };

                handle = GCHandle.Alloc(systemFileCacheInformation, GCHandleType.Pinned);

                if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemFileCacheInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(systemFileCacheInformation)) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                try
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
                catch (InvalidOperationException)
                {
                    // ignored
                }
            }

            var fileCacheSize = IntPtr.Subtract(IntPtr.Zero, 1); // Flush

            if (!NativeMethods.SetSystemFileCacheSize(fileCacheSize, fileCacheSize, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        /// <summary>
        /// Optimize the working set.
        /// </summary>
        private void OptimizeWorkingSet()
        {
            // Windows minimum version
            if (!OperatingSystem.HasWorkingSet)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.WorkingSet));

            var errors = new StringBuilder();

            var exclusionList = Settings.GetProcessExclusionSnapshot();

            if (exclusionList.Any())
            {
                // Check privilege
                if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeDebugName))
                    throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorAdminPrivilegeRequired, Constants.Windows.Privilege.SeDebugName));

                var processes = Process.GetProcesses().Where(process => process is not null && !exclusionList.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase));

                foreach (var process in processes)
                {
                    using (process)
                    {
                        try
                        {
                            if (!NativeMethods.EmptyWorkingSet(process.Handle))
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                        }
                        catch (InvalidOperationException)
                        {
                            // ignored
                        }
                        catch (Win32Exception e)
                        {
                            if (e.NativeErrorCode != Constants.Windows.SystemErrorCode.ErrorAccessDenied)
                                errors.Append(string.Format(Localizer.Culture, "{0}: {1} | ", process.ProcessName, e.GetMessage()));
                        }
                    }
                }

                if (errors.Length > 3)
                    errors.Remove(errors.Length - 3, 3);
            }
            else
            {
                // Check privilege
                if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName))
                    throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorAdminPrivilegeRequired, Constants.Windows.Privilege.SeProfSingleProcessName));

                var handle = GCHandle.Alloc(Constants.Windows.SystemMemoryListCommand.MemoryEmptyWorkingSets, GCHandleType.Pinned);

                try
                {
                    if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemMemoryListInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(Constants.Windows.SystemMemoryListCommand.MemoryEmptyWorkingSets)) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                    {
                        var e = new Win32Exception(Marshal.GetLastWin32Error());

                        if (e is not null)
                        {
                            if (errors.Length > 0)
                                errors.Append(" | ");

                            errors.Append(e.GetMessage());
                        }
                    }
                }
                finally
                {
                    try
                    {
                        if (handle.IsAllocated)
                            handle.Free();
                    }
                    catch (InvalidOperationException)
                    {
                        // ignored
                    }
                }
            }

            if (errors.Length > 0)
                throw new Exception(errors.ToString());
        }

        #endregion
    }
}
