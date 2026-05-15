using System;
using System.IO;
using System.Reflection;

namespace DustAdvisor.Hdt
{
    internal static class PluginPaths
    {
        public static string PluginRoot
        {
            get
            {
                var dll = Assembly.GetExecutingAssembly().Location;
                return Path.GetDirectoryName(dll) ?? string.Empty;
            }
        }

        public static string CacheDir => Path.Combine(PluginRoot, "cache");
        public static string UncraftableFile => Path.Combine(PluginRoot, "data", "uncraftable.json");
        public static string RefundFile => Path.Combine(PluginRoot, "data", "refund.json");
        public static string NeverSuggestFile => Path.Combine(PluginRoot, "data", "never_suggest.json");
        public static string MetaTiersFile => Path.Combine(PluginRoot, "data", "meta_tiers.json");
        public static string CardArtDir => Path.Combine(PluginRoot, "cache", "art");
    }
}
