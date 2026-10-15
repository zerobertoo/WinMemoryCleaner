using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
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
                var temp = Path.Combine(Path.GetTempPath(), exe);

                Helper.DeleteFile(temp);

                using (var response = await _client.GetAsync(Constants.App.Repository.LatestExeUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                    using (var destination = File.Create(temp))
                        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                }

                // Em single-file Assembly.GetName falha (BadImageFormat); a versão vem do recurso de versão do exe
                if (!File.Exists(temp) || !Version.TryParse(FileVersionInfo.GetVersionInfo(temp).FileVersion, out var downloadedVersion) || !downloadedVersion.Equals(newestVersion))
                    return;

                Process = new ProcessStartInfo
                {
                    Arguments = string.Format(CultureInfo.InvariantCulture, @"/c taskkill /f /im ""{0}"" & move /y ""{1}"" ""{2}"" & start """" ""{2}"" /{3} {4}", exe, temp, App.Path, newestVersion, string.Join(" ", args)),
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
                if (Settings.AutoUpdate && DateTimeOffset.Now.Subtract(_lastCheck).TotalHours < Constants.App.AutoUpdateInterval)
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
