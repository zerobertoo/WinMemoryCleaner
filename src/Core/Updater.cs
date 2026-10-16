using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Updater
    /// </summary>
    public static class Updater
    {
        // HttpClient é thread-safe e feito para ser reutilizado durante toda a vida do app
        private static readonly HttpClient _client = new HttpClient();
        private static CancellationTokenSource _cancellation;
        private static DateTimeOffset _lastCheck = DateTimeOffset.MinValue;

        internal static ProcessStartInfo Process;

        /// <summary>
        /// Consulta a versão mais recente, baixa o executável e prepara o comando de substituição.
        /// </summary>
        private static async Task CheckAndDownloadAsync(string[] args, CancellationToken cancellationToken)
        {
            try
            {
                var assemblyInfo = await _client.GetStringAsync(Constants.App.Repository.AssemblyInfoUri, cancellationToken).ConfigureAwait(false);
                var assemblyVersionMatch = Regex.Match(assemblyInfo, @"AssemblyVersion\(""(.*)""\)\]");

                if (!assemblyVersionMatch.Success)
                    return;

                var newestVersion = Version.Parse(assemblyVersionMatch.Groups[1].Value);

                if (App.Version >= newestVersion)
                    return;

                var exe = Path.GetFileName(App.Path);

                // Diretório exclusivo e imprevisível: evita que outro processo troque o arquivo entre a verificação e o move
                var tempDirectory = Path.Combine(Path.GetTempPath(), "WinMemoryCleaner-" + Guid.NewGuid().ToString("N"));
                var temp = Path.Combine(tempDirectory, exe);

                Directory.CreateDirectory(tempDirectory);

                try
                {
                    using (var response = await _client.GetAsync(Constants.App.Repository.LatestExeUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();

                        using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                        using (var destination = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                    }

                    // Integridade: o SHA-256 do exe baixado deve bater com o checksums.txt publicado na mesma release (falha fechada)
                    var checksums = await _client.GetStringAsync(Constants.App.Repository.ChecksumsUri, cancellationToken).ConfigureAwait(false);

                    if (!TryGetExpectedHash(checksums, Constants.App.Repository.ExecutableName, out var expectedHash) || !string.Equals(ComputeSha256(temp), expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Warning("Update discarded: downloaded executable does not match the published SHA-256 checksum");
                        Helper.DeleteFile(temp);
                        Directory.Delete(tempDirectory, true);

                        return;
                    }

                    // Em single-file Assembly.GetName falha (BadImageFormat); a versão vem do recurso de versão do exe
                    if (!Version.TryParse(FileVersionInfo.GetVersionInfo(temp).FileVersion, out var downloadedVersion) || !downloadedVersion.Equals(newestVersion))
                    {
                        Directory.Delete(tempDirectory, true);

                        return;
                    }
                }
                catch
                {
                    try { Directory.Delete(tempDirectory, true); } catch { /* ignored */ }

                    throw;
                }

                Process = new ProcessStartInfo
                {
                    Arguments = BuildReplaceCommand(exe, temp, App.Path, newestVersion.ToString(), BuildForwardedArguments(args), Constants.App.Name, tempDirectory, IsServiceRunning()),
                    CreateNoWindow = true,
                    FileName = "cmd",
                    RedirectStandardError = false,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = false,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                App.Shutdown();
            }
            catch (OperationCanceledException)
            {
                // cancelado por uma nova verificação
            }
            catch (Exception ex)
            {
                Logger.Error(ex);

                Reset();
            }
        }

        /// <summary>
        /// Indica se o serviço está em execução (só nesse caso ele é parado e reiniciado na atualização).
        /// </summary>
        private static bool IsServiceRunning()
        {
            try
            {
                using (var controller = new System.ServiceProcess.ServiceController(Constants.App.Name))
                    return controller.Status == System.ServiceProcess.ServiceControllerStatus.Running;
            }
            catch
            {
                // serviço não instalado
                return false;
            }
        }

        /// <summary>
        /// Monta a linha de comando <c>cmd /c</c> que substitui o executável e o reabre (função pura).
        /// </summary>
        /// <param name="restartService">Se verdadeiro, para o serviço antes de trocar o exe e o inicia depois; se falso, não o toca (estava parado ou não instalado)</param>
        public static string BuildReplaceCommand(string exe, string temp, string destination, string version, string forwardedArguments, string serviceName, string tempDirectory, bool restartService)
        {
            var stop = restartService ? string.Format(CultureInfo.InvariantCulture, @"net stop ""{0}"" >nul 2>&1 & ", serviceName) : string.Empty;
            var start = restartService ? string.Format(CultureInfo.InvariantCulture, @"net start ""{0}"" >nul 2>&1 & ", serviceName) : string.Empty;

            return string.Format(CultureInfo.InvariantCulture, @"/c {0}taskkill /f /im ""{1}"" >nul 2>&1 & move /y ""{2}"" ""{3}"" & rmdir ""{4}"" & {5}start """" ""{3}"" /{6} {7}", stop, exe, temp, destination, tempDirectory, start, version, forwardedArguments);
        }

        /// <summary>
        /// Calcula o SHA-256 (hex minúsculo) de um arquivo.
        /// </summary>
        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
                return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        /// <summary>
        /// Extrai de um checksums.txt (formato "hash  arquivo") o hash esperado para o arquivo informado.
        /// </summary>
        internal static bool TryGetExpectedHash(string checksums, string fileName, out string hash)
        {
            hash = null;

            foreach (var line in (checksums ?? string.Empty).Split('\n'))
            {
                var parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 2 && string.Equals(parts[1].TrimStart('*'), fileName, StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(parts[0], "^[0-9a-fA-F]{64}$"))
                {
                    hash = parts[0];

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Mantém apenas argumentos simples (letras, números, ponto e barra inicial) para repassar ao cmd sem risco de injeção.
        /// </summary>
        internal static string BuildForwardedArguments(string[] args)
        {
            return string.Join(" ", (args ?? Array.Empty<string>()).Where(argument => Regex.IsMatch(argument ?? string.Empty, @"^/?[A-Za-z0-9._]+\z")));
        }

        private static void Reset()
        {
            try
            {
                _cancellation?.Cancel();
                _cancellation?.Dispose();
            }
            catch
            {
                // ignored
            }
            finally
            {
                _cancellation = null;
            }

            Process = null;
        }

        /// <summary>
        /// Check for new version and update if available
        /// </summary>
        public static void Update(params string[] args)
        {
            try
            {
                // Respeita a opção do usuário (antes a condição estava invertida: com AutoUpdate desligado checava a cada chamada)
                if (!Settings.AutoUpdate || DateTimeOffset.Now.Subtract(_lastCheck).TotalHours < Constants.App.AutoUpdateInterval)
                    return;

                _lastCheck = DateTimeOffset.Now;

                Reset();

                _cancellation = new CancellationTokenSource();

                _ = CheckAndDownloadAsync(args, _cancellation.Token);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);

                Reset();
            }
        }
    }
}
