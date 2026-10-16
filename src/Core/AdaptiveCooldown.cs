using System;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Backoff adaptativo do gatilho por pouca memória: se otimizar não libera quase nada,
    /// dobra a espera até a próxima tentativa (até um teto) e volta ao normal quando há ganho
    /// </summary>
    public class AdaptiveCooldown
    {
        private const int MaxDoublings = 3;

        // ganho abaixo disso (fração da memória física total) conta como "sem ganho"
        private const double NegligibleGainFraction = 0.01;

        private int _consecutiveNoGain;

        /// <summary>
        /// Quantas otimizações seguidas ficaram sem ganho
        /// </summary>
        public int ConsecutiveNoGain => _consecutiveNoGain;

        /// <summary>
        /// Registra o ganho medido da última otimização
        /// </summary>
        /// <param name="releasedBytes">Memória física liberada (nunca negativa)</param>
        /// <param name="totalBytes">Memória física total</param>
        public void Record(ulong releasedBytes, ulong totalBytes)
        {
            if (totalBytes > 0 && releasedBytes < totalBytes * NegligibleGainFraction)
                _consecutiveNoGain = Math.Min(_consecutiveNoGain + 1, MaxDoublings);
            else
                _consecutiveNoGain = 0;
        }

        /// <summary>
        /// Espera efetiva em minutos: base dobrada a cada otimização sem ganho (teto de 8x)
        /// </summary>
        public double GetMinutes(double baseMinutes)
        {
            return baseMinutes * Math.Pow(2, _consecutiveNoGain);
        }
    }
}
