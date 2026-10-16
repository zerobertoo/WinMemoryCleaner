using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Decide se um processo está na lista de exclusão: nome exato ou padrão com <c>*</c> e <c>?</c> (lógica pura)
    /// </summary>
    public static class ProcessExclusion
    {
        /// <summary>
        /// Indica se o nome do processo (sem .exe) casa com alguma entrada, ignorando maiúsculas
        /// </summary>
        public static bool IsExcluded(string processName, IEnumerable<string> exclusions)
        {
            if (string.IsNullOrWhiteSpace(processName) || exclusions is null)
                return false;

            return exclusions.Any(entry => Matches(processName, entry));
        }

        private static bool Matches(string processName, string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
                return false;

            if (entry.IndexOfAny(new[] { '*', '?' }) < 0)
                return string.Equals(processName, entry, StringComparison.OrdinalIgnoreCase);

            var pattern = "^" + Regex.Escape(entry).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";

            return Regex.IsMatch(processName, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
