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
            var version = ReadVersion("30648b8d550465f4bb77f1e1afd0b37d");
            if(version.Major < 17 || (version.Major == 17 && version.Minor < 3))
                throw new System.NotSupportedException("Ho lilToon requires Unity 6000.3+ and HoURP 17.3+. Check the installed pipeline package.");
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
