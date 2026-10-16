using System.Collections.Generic;
using System.Linq;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Perfis predefinidos de áreas de memória (lógica pura). O perfil não é salvo: é deduzido das áreas marcadas.
    /// </summary>
    public static class OptimizationProfile
    {
        /// <summary>
        /// Perfis disponíveis; <see cref="Custom" /> é o resultado quando as áreas não coincidem com nenhum
        /// </summary>
        public enum Kind
        {
            Light,
            Balanced,
            Aggressive,
            Custom
        }

        private static readonly Dictionary<Kind, Enums.Memory.Areas> Definitions = new Dictionary<Kind, Enums.Memory.Areas>
        {
            [Kind.Light] = Enums.Memory.Areas.WorkingSet | Enums.Memory.Areas.StandbyListLowPriority,
            [Kind.Balanced] = Enums.Memory.Areas.WorkingSet | Enums.Memory.Areas.ModifiedPageList | Enums.Memory.Areas.SystemFileCache | Enums.Memory.Areas.StandbyListLowPriority | Enums.Memory.Areas.RegistryCache,
            [Kind.Aggressive] = Enums.Memory.Areas.CombinedPageList | Enums.Memory.Areas.ModifiedFileCache | Enums.Memory.Areas.ModifiedPageList | Enums.Memory.Areas.RegistryCache | Enums.Memory.Areas.StandbyList | Enums.Memory.Areas.SystemFileCache | Enums.Memory.Areas.WorkingSet
        };

        /// <summary>
        /// Áreas do perfil, limitadas às suportadas pelo sistema; <see cref="Custom" /> não tem áreas próprias
        /// </summary>
        public static Enums.Memory.Areas GetAreas(Kind kind, Enums.Memory.Areas supported)
        {
            return Definitions.TryGetValue(kind, out var areas) ? areas & supported : Enums.Memory.Areas.None;
        }

        /// <summary>
        /// Deduz o perfil a partir das áreas marcadas; sem correspondência exata é <see cref="Kind.Custom" />
        /// </summary>
        public static Kind Detect(Enums.Memory.Areas current, Enums.Memory.Areas supported)
        {
            foreach (var kind in Definitions.Keys.Where(kind => GetAreas(kind, supported) == current && current != Enums.Memory.Areas.None))
                return kind;

            return Kind.Custom;
        }
    }
}
