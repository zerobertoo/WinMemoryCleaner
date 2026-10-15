using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Windows Memory Cleaner Service Installer (uses sc.exe, since System.Configuration.Install does not exist in modern .NET)
    /// </summary>
    public static class WinServiceInstaller
    {
        /// <summary>
        /// Installs the service
        /// </summary>
        public static void Install()
        {
            Uninstall();

            var executablePath = Environment.ProcessPath;

            RunServiceControl("create", Constants.App.Name,
                "binPath=", "\"" + executablePath + "\" /Service",
                "start=", "delayed-auto",
                "DisplayName=", Constants.App.Title,
                "obj=", "LocalSystem");

            RunServiceControl("description", Constants.App.Name, Constants.App.Repository.Uri.ToString());

            // Service recommended settings
            Settings.AutoOptimizationInterval = Settings.AutoOptimizationInterval == 0 ? 24 : Settings.AutoOptimizationInterval;
            Settings.AutoOptimizationMemoryUsage = Settings.AutoOptimizationMemoryUsage == 0 ? 10 : Settings.AutoOptimizationMemoryUsage;
            Settings.MemoryAreas = Enums.Memory.Areas.CombinedPageList | Enums.Memory.Areas.ModifiedFileCache | Enums.Memory.Areas.ModifiedPageList | Enums.Memory.Areas.RegistryCache | Enums.Memory.Areas.StandbyList | Enums.Memory.Areas.SystemFileCache | Enums.Memory.Areas.WorkingSet;
            Settings.RunOnPriority = Enums.Priority.Low;
            Settings.RunOnStartup = false;
            Settings.ShowOptimizationNotifications = false;
            Settings.Save();

            // Remove run on startup
            App.RunOnStartup(false);

            // Start service after install
            using (var sc = new ServiceController(Constants.App.Name))
                sc.Start();
        }

        /// <summary>
        /// Uninstalls the service
        /// </summary>
        public static void Uninstall()
        {
            if (!WinService.IsInstalled)
                return;

            // Processes that block service refresh/uninstallation
            var processesToKill = new[] { "mmc", "procexp", "procexp64", "taskmgr" };

            KillProcesses(Process.GetProcesses().Where(process => process != null && processesToKill.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase)));

            try
            {
                using (var sc = new ServiceController(Constants.App.Name))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                }
            }
            catch
            {
                // ignored
            }

            RunServiceControl("delete", Constants.App.Name);

            // Kill any remaining process (except the current one)
            var currentProcessId = Process.GetCurrentProcess().Id;

            KillProcesses(Process.GetProcessesByName(Constants.App.Name).Where(process => process.Id != currentProcessId));
        }

        private static void KillProcesses(System.Collections.Generic.IEnumerable<Process> processes)
        {
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static void RunServiceControl(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo("sc.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using (var process = Process.Start(startInfo))
                process.WaitForExit();
        }
    }
}
