using System;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Decide se uma otimização automática deve rodar agora (lógica pura, compartilhada entre GUI e serviço)
    /// </summary>
    public static class AutoOptimizationTrigger
    {
        /// <summary>
        /// Decide o gatilho da otimização automática; o intervalo tem prioridade sobre o uso de memória
        /// </summary>
        /// <param name="now">Instante atual</param>
        /// <param name="intervalHours">Intervalo configurado em horas (0 = desligado)</param>
        /// <param name="lastByInterval">Última otimização por intervalo</param>
        /// <param name="memoryUsageThreshold">Limite de memória livre em % (0 = desligado)</param>
        /// <param name="freePercentage">Percentual de memória física livre</param>
        /// <param name="lastByMemoryUsage">Última otimização por pouca memória</param>
        /// <param name="memoryUsageCooldownMinutes">Espera mínima entre otimizações por pouca memória</param>
        /// <returns>O motivo da otimização, ou nulo se nada deve rodar</returns>
        public static Enums.Memory.Optimization.Reason? Decide(
            DateTimeOffset now,
            int intervalHours,
            DateTimeOffset lastByInterval,
            int memoryUsageThreshold,
            double freePercentage,
            DateTimeOffset lastByMemoryUsage,
            double memoryUsageCooldownMinutes)
        {
            if (intervalHours > 0 && now.Subtract(lastByInterval).TotalHours >= intervalHours)
                return Enums.Memory.Optimization.Reason.Schedule;

            if (memoryUsageThreshold > 0 && freePercentage < memoryUsageThreshold && now.Subtract(lastByMemoryUsage).TotalMinutes >= memoryUsageCooldownMinutes)
                return Enums.Memory.Optimization.Reason.LowMemory;

            return null;
        }
    }
}
