using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Entrada do histórico: uma otimização concluída
    /// </summary>
    public class OptimizationHistoryEntry
    {
        public DateTimeOffset Timestamp { get; set; }

        public string Reason { get; set; }

        public long ReleasedBytes { get; set; }

        public double DurationSeconds { get; set; }

        public string[] SucceededAreas { get; set; } = Array.Empty<string>();

        public string[] FailedAreas { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Histórico local de otimizações (um JSON por linha, sem dependências); mantém só as mais recentes
    /// </summary>
    public static class OptimizationHistory
    {
        private const int MaxEntries = 500;
        private static readonly object _lock = new object();

        /// <summary>
        /// Arquivo padrão, compartilhado entre GUI, serviço e linha de comando
        /// </summary>
        public static string DefaultPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Constants.App.Name, "history.jsonl");

        /// <summary>
        /// Acrescenta uma entrada; nunca lança (histórico não pode quebrar a otimização)
        /// </summary>
        public static void Append(OptimizationHistoryEntry entry, string path = null)
        {
            try
            {
                path ??= DefaultPath;

                lock (_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                    var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();

                    lines.Add(JsonSerializer.Serialize(entry));

                    if (lines.Count > MaxEntries)
                        lines = lines.Skip(lines.Count - MaxEntries).ToList();

                    File.WriteAllLines(path, lines);
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e.Message);
            }
        }

        /// <summary>
        /// Lê as entradas, da mais antiga para a mais recente; linhas inválidas são ignoradas
        /// </summary>
        public static List<OptimizationHistoryEntry> Read(string path = null)
        {
            var entries = new List<OptimizationHistoryEntry>();

            try
            {
                path ??= DefaultPath;

                if (!File.Exists(path))
                    return entries;

                lock (_lock)
                {
                    foreach (var line in File.ReadAllLines(path))
                    {
                        try
                        {
                            var entry = JsonSerializer.Deserialize<OptimizationHistoryEntry>(line);

                            if (entry is not null)
                                entries.Add(entry);
                        }
                        catch (JsonException)
                        {
                            // linha corrompida: ignora
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e.Message);
            }

            return entries;
        }

        /// <summary>
        /// Converte o resultado de uma otimização em entrada de histórico
        /// </summary>
        public static OptimizationHistoryEntry FromResult(OptimizationResult result, string reason, long releasedBytes, DateTimeOffset now)
        {
            return new OptimizationHistoryEntry
            {
                Timestamp = now,
                Reason = reason,
                ReleasedBytes = Math.Max(0, releasedBytes),
                DurationSeconds = result.Areas.Sum(area => area.Elapsed.TotalSeconds),
                SucceededAreas = result.Areas.Where(area => area.Succeeded).Select(area => area.Name).ToArray(),
                FailedAreas = result.Failed.Select(area => area.Name).ToArray()
            };
        }
    }
}
