#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    public class lilRenderPipelineReader
    {
        public static lilRenderPipeline GetRP()
        {
            return lilRenderPipeline.URP;
        }

        public static PackageVersionInfos GetRPInfos()
        {
            return GetURPVersion();
        }

        private static PackageVersionInfos GetURPVersion()
        {
            string path = AssetDatabase.GUIDToAssetPath("30648b8d550465f4bb77f1e1afd0b37d");
            var package = File.Exists(path) ? JsonUtility.FromJson<PackageInfos>(File.ReadAllText(path)) : new PackageInfos();
            string guid =
                package.displayName.Contains("SLZ") ?
                "753d1ac2429a21a44ac5f937cbbb409f" : // Core
                "30648b8d550465f4bb77f1e1afd0b37d";  // URP
            var version = ReadVersion(guid);
            version.RP = lilRenderPipeline.URP;
            return version;
        }

        private static PackageVersionInfos ReadVersion(string guid)
        {
            string version = "";
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if(!string.IsNullOrEmpty(path))
            {
                var package = JsonUtility.FromJson<PackageInfos>(File.ReadAllText(path));
                version = package.version;
            }

            PackageVersionInfos infos;
            infos.RP = lilRenderPipeline.URP;
            if(string.IsNullOrEmpty(version))
            {
                infos.Major = 0;
                infos.Minor = 0;
                infos.Patch = 0;
            }
            else
            {
                var parser = new SemVerParser(version);
                infos.Major = parser.major;
                infos.Minor = parser.minor;
                infos.Patch = parser.patch;
            }
            return infos;
        }

        private class PackageInfos
        {
            public string displayName = "";
            public string version = "";
        }
    }

    public struct PackageVersionInfos
    {
        public lilRenderPipeline RP;
        public int Major;
        public int Minor;
        public int Patch;
    }
}
#endif
