using System;
using System.Windows;

namespace DustAdvisor.Ui
{
    public static class Localization
    {
        private static bool _loaded;
        private static string _language;

        public static string CurrentLanguage => _language ?? "en";

        public static string NormalizeLanguage(string hdtLocale)
        {
            if (string.IsNullOrEmpty(hdtLocale)) return "en";
            return hdtLocale.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "es" : "en";
        }

        public static void EnsureLoaded(string hdtLocale)
        {
            if (_loaded) return;
            _language = NormalizeLanguage(hdtLocale);
            var app = Application.Current;
            if (app == null) return;
            var uri = new Uri(
                "pack://application:,,,/DustAdvisor.Ui;component/Themes/Strings." + _language + ".xaml",
                UriKind.Absolute);
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
            _loaded = true;
        }

        public static string Get(string key)
        {
            var app = Application.Current;
            if (app == null) return key;
            return app.Resources[key] as string ?? key;
        }

        public static string Format(string key, params object[] args)
        {
            var template = Get(key);
            if (args == null || args.Length == 0) return template;
            try { return string.Format(template, args); }
            catch (FormatException) { return template; }
        }
    }
}
