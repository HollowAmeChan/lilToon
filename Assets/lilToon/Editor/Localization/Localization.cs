using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    internal partial class L10n : ScriptableSingleton<L10n>
    {
        public LocalizationAsset localizationAssetFallback;
        public LocalizationAsset localizationAsset;
        private static string[] languages;
        private static string[] languageNames;
        private static readonly Dictionary<string, GUIContent> guicontents = new();
        private static string localizationFolder => AssetDatabase.GUIDToAssetPath("2feb2bcbf5b4ef043910b310c21b6ba7");

        internal static void Load()
        {
            guicontents.Clear();
            var path = localizationFolder + "/" + ResolveLanguage(Settings.instance.language) + ".po";
            // 路径必须交给 AssetDatabase，不能先 File.Exists 拦一道：包以 "file:" 或注册表
            // 方式安装时 GUIDToAssetPath 返回 "Packages/<name>/..." 这种虚拟路径，它对
            // System.IO 是不可见的（File.Exists 恒为 false），会让选中的语言文件永远加载不上，
            // 于是一直静默回退到 en-US，新增的 key 则直接显示成原始 key。
            instance.localizationAsset = AssetDatabase.LoadAssetAtPath<LocalizationAsset>(path);

            if(!instance.localizationAssetFallback) instance.localizationAssetFallback = AssetDatabase.LoadAssetAtPath<LocalizationAsset>(localizationFolder + "/en-US.po");
            if(!instance.localizationAsset) instance.localizationAsset = new LocalizationAsset();
        }

        // Settings.language 的默认值是 CultureInfo.CurrentCulture.Name，中文系统上是 "zh-CN"，
        // 而包里的文件名是 "zh-Hans" / "zh-Hant"，直接拼路径永远命中不了。这里按实际存在的
        // .po 列表把区域名映射过去。
        internal static string ResolveLanguage(string language)
        {
            var available = GetLanguages();
            if(available.Length == 0) return language;
            if(string.IsNullOrEmpty(language)) return "en-US";
            if(available.Contains(language)) return language;

            if(language.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase))
            {
                bool hant = language.IndexOf("Hant", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            language.EndsWith("TW", System.StringComparison.OrdinalIgnoreCase) ||
                            language.EndsWith("HK", System.StringComparison.OrdinalIgnoreCase) ||
                            language.EndsWith("MO", System.StringComparison.OrdinalIgnoreCase);
                var zh = hant ? "zh-Hant" : "zh-Hans";
                if(available.Contains(zh)) return zh;
            }

            // 其它语言退到同语系的第一个："ja" -> "ja-JP"
            var neutral = language.Split('-')[0];
            var match = available.FirstOrDefault(l => l.StartsWith(neutral + "-", System.StringComparison.OrdinalIgnoreCase));
            return match ?? "en-US";
        }

        internal static string[] GetLanguages()
        {
            if(languages != null) return languages;

            // AssetDatabase 是唯一对「包安装」和「放在 Assets/ 下」都成立的入口：
            // 包内路径是 "Packages/<name>/..." 虚拟路径，System.IO 完全看不见它
            // （Directory.GetFiles 会抛 DirectoryNotFoundException）。System.IO 只作为
            // 兜底，留给 Assets/ 下那种真实目录的老式安装。
            IEnumerable<string> found = string.IsNullOrEmpty(localizationFolder)
                ? new string[0]
                : AssetDatabase.FindAssets(string.Empty, new[]{ localizationFolder })
                    .Select(g => AssetDatabase.GUIDToAssetPath(g))
                    .Where(p => p.EndsWith(".po", System.StringComparison.OrdinalIgnoreCase))
                    .Select(p => Path.GetFileNameWithoutExtension(p));

            if(!found.Any() && Directory.Exists(localizationFolder))
                found = Directory.GetFiles(localizationFolder, "*.po").Select(f => Path.GetFileNameWithoutExtension(f));

            return languages = found.Where(f => !string.IsNullOrEmpty(f) && !f.StartsWith("._"))
                                    .OrderBy(f => f, System.StringComparer.Ordinal)
                                    .ToArray();
        }

        internal static string[] GetLanguageNames()
        {
            return languageNames ??= GetLanguages().Select(l => {
                if(l == "zh-Hans") return "简体中文";
                if(l == "zh-Hant") return "繁體中文";
                return new CultureInfo(l).NativeName;
            }).ToArray();
        }

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
