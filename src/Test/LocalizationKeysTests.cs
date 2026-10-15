using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace WinMemoryCleaner.Test
{
    /// <summary>
    /// Garante que todos os idiomas possuem as mesmas chaves e placeholders do English.json.
    /// </summary>
    [TestFixture]
    public class LocalizationKeysTests
    {
        private const string ReferenceLanguage = "English";

        private static Dictionary<string, Dictionary<string, string>> LoadAllLanguages()
        {
            var assembly = typeof(Localization).Assembly;
            var languages = new Dictionary<string, Dictionary<string, string>>();

            foreach (var resource in assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(Constants.App.LocalizationResourcePath, StringComparison.OrdinalIgnoreCase)
                            && name.EndsWith(Constants.App.EmbeddedResourcePathExtension, StringComparison.OrdinalIgnoreCase)))
            {
                var language = resource.Substring(Constants.App.LocalizationResourcePath.Length);
                language = language.Substring(0, language.Length - Constants.App.EmbeddedResourcePathExtension.Length);

                using (var stream = assembly.GetManifestResourceStream(resource))
                using (var document = JsonDocument.Parse(stream))
                {
                    languages[language] = document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
                }
            }

            return languages;
        }

        private static string Placeholders(string value)
        {
            return string.Join(",", Regex.Matches(value ?? string.Empty, @"\{\d+[^}]*\}").Cast<Match>().Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal));
        }

        [Test]
        public void AllLanguages_HaveSameKeysAsEnglish()
        {
            var languages = LoadAllLanguages();
            Assert.IsTrue(languages.ContainsKey(ReferenceLanguage), "English resource not found");

            var reference = languages[ReferenceLanguage].Keys.ToList();
            var problems = new List<string>();

            foreach (var language in languages.Where(l => l.Key != ReferenceLanguage))
            {
                var missing = reference.Except(language.Value.Keys).ToList();
                var orphans = language.Value.Keys.Except(reference).ToList();

                if (missing.Any())
                    problems.Add(language.Key + " missing: " + string.Join(", ", missing));

                if (orphans.Any())
                    problems.Add(language.Key + " orphans: " + string.Join(", ", orphans));

                var empty = language.Value.Where(p => string.IsNullOrWhiteSpace(p.Value)).Select(p => p.Key).ToList();

                if (empty.Any())
                    problems.Add(language.Key + " empty: " + string.Join(", ", empty));
            }

            Assert.IsEmpty(problems, string.Join(Environment.NewLine, problems));
        }

        [Test]
        public void AllLanguages_HaveSamePlaceholdersAsEnglish()
        {
            var languages = LoadAllLanguages();
            var reference = languages[ReferenceLanguage];
            var problems = new List<string>();

            foreach (var language in languages.Where(l => l.Key != ReferenceLanguage))
                foreach (var pair in language.Value.Where(p => reference.ContainsKey(p.Key)))
                    if (Placeholders(pair.Value) != Placeholders(reference[pair.Key]))
                        problems.Add(language.Key + "." + pair.Key);

            Assert.IsEmpty(problems, "Placeholder mismatch: " + string.Join(", ", problems));
        }

        [Test]
        public void English_MatchesLocalizationModelProperties()
        {
            var keys = LoadAllLanguages()[ReferenceLanguage].Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var properties = typeof(Localization).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string)).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

            CollectionAssert.AreEqual(properties, keys);
        }
    }
}
