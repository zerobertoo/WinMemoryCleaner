using System;
using System.Collections.Generic;
using System.Linq;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Resultado da otimização de uma única área de memória
    /// </summary>
    public class OptimizationAreaResult
    {
        /// <summary>
        /// Nome (localizado) da área
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Tempo gasto na área
        /// </summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>
        /// Mensagem de erro; nulo quando a área foi otimizada com sucesso
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Indica se a área foi otimizada sem erro
        /// </summary>
        public bool Succeeded => Error is null;
    }

    /// <summary>
    /// Resultado de uma otimização completa, com o detalhe por área
    /// </summary>
    public class OptimizationResult
    {
        /// <summary>
        /// Resultado vazio: nada foi executado (sem áreas ou outra otimização em curso)
        /// </summary>
        public static OptimizationResult Skipped => new OptimizationResult();

        /// <summary>
        /// Resultado de cada área executada
        /// </summary>
        public List<OptimizationAreaResult> Areas { get; } = new List<OptimizationAreaResult>();

        /// <summary>
        /// Indica se ao menos uma área foi executada (falso quando a otimização foi descartada)
        /// </summary>
        public bool WasExecuted => Areas.Count > 0;

        /// <summary>
        /// Áreas que falharam
        /// </summary>
        public IEnumerable<OptimizationAreaResult> Failed => Areas.Where(area => !area.Succeeded);

        /// <summary>
        /// Indica se ao menos uma área falhou
        /// </summary>
        public bool HasFailures => Areas.Any(area => !area.Succeeded);

        /// <summary>
        /// Indica se houve sucesso parcial (parte das áreas falhou, parte funcionou)
        /// </summary>
        public bool IsPartial => HasFailures && Areas.Any(area => area.Succeeded);
    }
}
