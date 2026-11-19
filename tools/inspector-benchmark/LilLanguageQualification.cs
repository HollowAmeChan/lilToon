using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;

internal static class LilLanguageQualification
{
    internal static string Run()
    {
        var assembly = Assembly.Load("lilToon.Editor");
        var manager = assembly.GetType("lilToon.lilLanguageManager", true);
        var inspector = assembly.GetType("lilToon.lilToonInspector", true);
        var update = manager.GetMethod("UpdateLanguage");
        var getLoc = manager.GetMethod("GetLoc");
        var loadCustom = inspector.GetMethod("LoadCustomLanguage");
        var langSet = manager.GetProperty("langSet").GetValue(null);
        var langCode = langSet.GetType().GetProperty("languageName");
        var columnIndex = langSet.GetType().GetProperty("languageNum");
        var settingsType = assembly.GetType("lilToon.Settings", true);
        var localization = assembly.GetType("lilToon.L10n", true);
        var settings = settingsType.BaseType.GetProperty("instance").GetValue(null);
        var languageField = settingsType.GetField("language");
        var selectedLanguage = (string)languageField.GetValue(settings);
        const string folder = "Packages/jp.lilxyzw.liltoon/Editor/Localization";
        const string testKey = "lilToon.Qualification.CustomTranslation";
        const string packageTestKey = "lilToon.Qualification.PackageTranslation";
        const string localTestPath = "Assets/LilLanguageQualification.txt";
        const string packageTestPath = folder + "/LilLanguageQualification.txt";
        const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.NonPublic;
        var cases = new (string Input, string Expected, int Index, string Translation, string Custom)[]
        {
            ("en-US", "en-US", 0, "Main Color", "English"),
            ("en-GB", "en-US", 0, "Main Color", "English"),
            ("zh", "zh-Hans", 3, "主色", "简体"),
            ("zh-CN", "zh-Hans", 3, "主色", "简体"),
            ("zh-SG", "zh-Hans", 3, "主色", "简体"),
            ("zh-Hans", "zh-Hans", 3, "主色", "简体"),
            ("zh-Hans-HK", "zh-Hans", 3, "主色", "简体"),
            ("zh-TW", "zh-Hans", 3, "主色", "简体"),
            ("zh-HK", "zh-Hans", 3, "主色", "简体"),
            ("zh-MO", "zh-Hans", 3, "主色", "简体"),
            ("ZH-hANT", "zh-Hans", 3, "主色", "简体"),
            ("zh-Hant-CN", "zh-Hans", 3, "主色", "简体"),
            ("ja-JP", "en-US", 0, "Main Color", "English"),
            ("ko-KR", "en-US", 0, "Main Color", "English"),
            (null, "en-US", 0, "Main Color", "English"),
            ("", "en-US", 0, "Main Color", "English")
        };

        var codes = (string[])localization.GetMethod("GetLanguages", staticFlags).Invoke(null, null);
        var labels = (string[])localization.GetMethod("GetLanguageNames", staticFlags).Invoke(null, null);
        Check(codes.SequenceEqual(new[] { "en-US", "zh-Hans" }), "language choices");
        Check(labels.SequenceEqual(new[] { "English", "简体中文" }), "language display names");
        foreach (var code in codes)
            Check(AssetDatabase.LoadMainAssetAtPath(folder + "/" + code + ".po") != null, "missing package translation " + code);
        foreach (var code in new[] { "ja-JP", "ko-KR", "zh-Hant" })
            Check(AssetDatabase.LoadMainAssetAtPath(folder + "/" + code + ".po") == null, "retired translation still exists " + code);

        var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(packageTestPath);
        Check(package != null, "test package lookup");
        var packageFile = Path.Combine(package.resolvedPath, packageTestPath.Substring(package.assetPath.Length + 1));

        try
        {
            const string columns = "Key\tEnglish\tJapanese\tKorean\tChinese Simplified\tChinese Traditional\n";
            const string values = "\tEnglish\t日語\t한국어\t简体\t繁體\n";
            File.WriteAllText(localTestPath, columns + testKey + values);
            File.WriteAllText(packageFile, columns + packageTestKey + values);
            AssetDatabase.ImportAsset(localTestPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(packageTestPath, ImportAssetOptions.ForceSynchronousImport);
            var guid = AssetDatabase.AssetPathToGUID(localTestPath);
            var packageGuid = AssetDatabase.AssetPathToGUID(packageTestPath);
            Check(!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(packageGuid), "TSV fixtures imported");

            foreach (var test in cases)
            {
                languageField.SetValue(settings, test.Input);
                update.Invoke(null, null);
                Check((string)langCode.GetValue(langSet) == test.Expected, "locale " + test.Input);
                Check((int)columnIndex.GetValue(langSet) == test.Index, "legacy column " + test.Input);
                Check((string)getLoc.Invoke(null, new object[] { "sMainColor" }) == test.Translation, "package translation " + test.Input);
                loadCustom.Invoke(null, new object[] { guid });
                loadCustom.Invoke(null, new object[] { packageGuid });
                Check((string)getLoc.Invoke(null, new object[] { testKey }) == test.Custom, "custom translation " + test.Input);
                Check((string)getLoc.Invoke(null, new object[] { packageTestKey }) == test.Custom, "custom package translation " + test.Input);
            }
        }
        finally
        {
            languageField.SetValue(settings, selectedLanguage);
            update.Invoke(null, null);
            AssetDatabase.DeleteAsset(localTestPath);
            if (!AssetDatabase.DeleteAsset(packageTestPath))
            {
                File.Delete(packageFile);
                File.Delete(packageFile + ".meta");
            }
        }

        return "verified " + cases.Length + " locales, both TSV paths, and 2 package .po assets";
    }

    private static void Check(bool valid, string description)
    {
        if (!valid) throw new InvalidOperationException(description);
    }
}
