using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    internal partial class L10n : ScriptableSingleton<L10n>
    {
        public LocalizationAsset localizationAssetFallback;
        public LocalizationAsset localizationAsset;
        internal const string English = "en-US";
        internal const string SimplifiedChinese = "zh-Hans";
        private static readonly string[] languages = { English, SimplifiedChinese };
        private static readonly string[] languageNames = { "English", "简体中文" };
        private static readonly Dictionary<string, GUIContent> guicontents = new();
        private static string localizationFolder => AssetDatabase.GUIDToAssetPath("2feb2bcbf5b4ef043910b310c21b6ba7");

        internal static void Load()
        {
            guicontents.Clear();
            // AssetDatabase resolves the virtual paths used by package installations.
            var folder = localizationFolder;
            var language = ResolveLanguage(Settings.instance.language);
            if(!instance.localizationAssetFallback) instance.localizationAssetFallback = AssetDatabase.LoadAssetAtPath<LocalizationAsset>(folder + "/" + English + ".po");
            instance.localizationAsset = language == English
                ? instance.localizationAssetFallback
                : AssetDatabase.LoadAssetAtPath<LocalizationAsset>(folder + "/" + language + ".po");
            if(!instance.localizationAsset) instance.localizationAsset = new LocalizationAsset();
        }

        internal static string ResolveLanguage(string language)
        {
            const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
            return string.Equals(language, "zh", comparison) ||
                   language != null && language.StartsWith("zh-", comparison)
                ? SimplifiedChinese
                : English;
        }

        internal static string[] GetLanguages() => languages;
        internal static string[] GetLanguageNames() => languageNames;

        internal static string L(string key)
        {
            if (!instance.localizationAsset || !instance.localizationAssetFallback) Load();
            var localized = instance.localizationAsset.GetLocalizedString(key);
            return localized != key ? localized : instance.localizationAssetFallback.GetLocalizedString(key);
        }

        internal static GUIContent G(string key) => G(key, null, "");
        private static GUIContent G(string[] key) => key.Length == 2 ? G(key[0], null, key[1]) : G(key[0], null, null);
        internal static GUIContent G(string key, string tooltip) => G(key, null, tooltip); // From EditorToolboxSettings
        private static GUIContent G(string key, Texture image) => G(key, image, "");

        private static GUIContent G(string key, Texture image, string tooltip)
        {
            if (!instance.localizationAsset || !instance.localizationAssetFallback) Load();
            if (guicontents.TryGetValue(key, out var content)) return content;
            return guicontents[key] = new GUIContent(L(key), image, L(tooltip));
        }
    }
}
