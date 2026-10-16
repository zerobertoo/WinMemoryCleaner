using System;
using System.Linq;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Interpreta os argumentos de linha de comando (lógica pura, sem efeitos colaterais)
    /// </summary>
    public static class CommandLineParser
    {
        /// <summary>
        /// Resultado da interpretação dos argumentos
        /// </summary>
        public class Result
        {
            /// <summary>
            /// Áreas de memória pedidas na linha de comando
            /// </summary>
            public Enums.Memory.Areas MemoryAreas { get; set; } = Enums.Memory.Areas.None;

            /// <summary>
            /// Modo de inicialização
            /// </summary>
            public Enums.StartupType StartupType { get; set; } = Enums.StartupType.App;

            /// <summary>
            /// Indica que o argumento com a versão atual foi recebido (o app acabou de ser atualizado)
            /// </summary>
            public bool UpdatedToCurrentVersion { get; set; }
        }

        /// <summary>
        /// Normaliza os argumentos: troca "-" por "/" e remove espaços
        /// </summary>
        public static string[] Normalize(string[] arguments)
        {
            return arguments?.Select(argument => argument.Replace("-", "/").Trim()).ToArray();
        }

        /// <summary>
        /// Interpreta os argumentos já normalizados; o último argumento de modo vence
        /// </summary>
        /// <param name="normalizedArguments">Argumentos retornados por <see cref="Normalize" /></param>
        /// <param name="currentVersion">Texto da versão atual, para detectar o argumento de pós-atualização</param>
        public static Result Parse(string[] normalizedArguments, string currentVersion)
        {
            var result = new Result();

            if (normalizedArguments is null)
                return result;

            foreach (var argument in normalizedArguments.Select(arg => arg.Replace("/", string.Empty)))
            {
                // Memory areas to optimize
                if (Enum.TryParse(argument, true, out Enums.Memory.Areas area))
                    result.MemoryAreas |= area;

                // Startup Type
                if (result.MemoryAreas != Enums.Memory.Areas.None)
                    result.StartupType = Enums.StartupType.Silent;

                if (argument.Equals(Constants.App.CommandLineArgument.Install, StringComparison.OrdinalIgnoreCase))
                    result.StartupType = Enums.StartupType.Installation;

                if (argument.Equals(Constants.App.CommandLineArgument.Reset, StringComparison.OrdinalIgnoreCase))
                    result.StartupType = Enums.StartupType.Reset;

                if (argument.Equals(Constants.App.CommandLineArgument.Service, StringComparison.OrdinalIgnoreCase))
                    result.StartupType = Enums.StartupType.Service;

                if (argument.Equals(Constants.App.CommandLineArgument.Uninstall, StringComparison.OrdinalIgnoreCase))
                    result.StartupType = Enums.StartupType.Uninstallation;

                if (argument.Equals(currentVersion))
                    result.UpdatedToCurrentVersion = true;
            }

            return result;
        }
    }
}
