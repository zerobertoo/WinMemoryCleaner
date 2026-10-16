namespace WinMemoryCleaner
{
    /// <summary>
    /// IMemoryService
    /// </summary>
    public interface IMemoryService
    {
        /// <summary>
        /// Gets the memory info (RAM)
        /// </summary>
        Memory Memory { get; }

        /// <summary>
        /// Optimize the computer
        /// </summary>
        /// <param name="reason">Optimization reason</param>
        /// <param name="areas">Memory areas</param>
        /// <returns>Resultado por área; vazio quando nada foi executado</returns>
        OptimizationResult Optimize(Enums.Memory.Optimization.Reason reason, Enums.Memory.Areas areas);

        /// <summary>
        /// Simula a otimização sem alterar a memória (ver quais áreas rodariam)
        /// </summary>
        /// <param name="areas">Memory areas</param>
        OptimizationResult DryRun(Enums.Memory.Areas areas);
    }
}
