using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;

namespace DustAdvisor.Ui.Tests
{
    public class LocalizationTests
    {
        private const string XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

        [Fact]
        public void En_And_Es_String_Dictionaries_Have_Identical_Key_Sets()
        {
            var enKeys = LoadKeys("Strings.en.xaml");
            var esKeys = LoadKeys("Strings.es.xaml");

            var missingInEs = enKeys.Except(esKeys).ToList();
            var extraInEs = esKeys.Except(enKeys).ToList();

            using var _ = new FluentAssertions.Execution.AssertionScope();
            missingInEs.Should().BeEmpty("every English key must have a Spanish translation");
            extraInEs.Should().BeEmpty("every Spanish key must correspond to an English source key");
        }

        [Fact]
        public void Format_Keys_Have_Matching_Placeholder_Counts_Across_Languages()
        {
            var enStrings = LoadAll("Strings.en.xaml");
            var esStrings = LoadAll("Strings.es.xaml");

            foreach (var kv in enStrings.Where(kv => kv.Key.EndsWith("_Fmt")))
            {
                int enCount = CountPlaceholders(kv.Value);
                int esCount = CountPlaceholders(esStrings[kv.Key]);
                esCount.Should().Be(enCount,
                    $"key '{kv.Key}' must have the same number of {{N}} placeholders in both languages");
            }
        }

        [Theory]
        [InlineData("DA_Title_Fmt")]
        [InlineData("DA_Help_Title")]
        [InlineData("DA_Error_NoCollection_Caption")]
        public void Critical_Keys_Are_Present_In_Both_Languages(string key)
        {
            var en = LoadAll("Strings.en.xaml");
            var es = LoadAll("Strings.es.xaml");
            en.ContainsKey(key).Should().BeTrue($"English dictionary must define '{key}'");
            es.ContainsKey(key).Should().BeTrue($"Spanish dictionary must define '{key}'");
            en[key].Should().NotBeNullOrWhiteSpace();
            es[key].Should().NotBeNullOrWhiteSpace();
        }

        private static HashSet<string> LoadKeys(string fileName) =>
            new HashSet<string>(LoadAll(fileName).Keys);

        private static Dictionary<string, string> LoadAll(string fileName)
        {
            var path = ResolveStringsPath(fileName);
            File.Exists(path).Should().BeTrue($"strings file should exist at {path}");
            var doc = XDocument.Load(path);
            var keyAttr = XName.Get("Key", XamlNs);
            return doc.Descendants()
                .Where(e => e.Attribute(keyAttr) != null)
                .ToDictionary(
                    e => e.Attribute(keyAttr)!.Value,
                    e => e.Value);
        }

        private static string ResolveStringsPath(string fileName)
        {
            // Test bin: src/DustAdvisor.Ui.Tests/bin/{Cfg}/net8.0/
            // Strings:  src/DustAdvisor.Ui/Themes/{fileName}
            var baseDir = System.AppContext.BaseDirectory;
            return Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..",
                "DustAdvisor.Ui", "Themes", fileName));
        }

        private static int CountPlaceholders(string template)
        {
            if (string.IsNullOrEmpty(template)) return 0;
            var indices = new HashSet<int>();
            for (int i = 0; i < template.Length - 2; i++)
            {
                if (template[i] != '{') continue;
                if (template[i + 1] == '{') { i++; continue; }
                int j = i + 1;
                int n = 0;
                bool any = false;
                while (j < template.Length && char.IsDigit(template[j]))
                {
                    n = n * 10 + (template[j] - '0');
                    any = true;
                    j++;
                }
                if (any && j < template.Length && (template[j] == '}' || template[j] == ':'))
                    indices.Add(n);
            }
            return indices.Count;
        }
    }
}
