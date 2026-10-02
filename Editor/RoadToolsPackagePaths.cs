using System;
using System.IO;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadToolsPackagePaths
    {
        internal const string PackageName = "com.denx4x.road-tools";
        internal const string GeneratedRoot = "Assets/Road Tools/Generated";

        internal static string SourceRoot
        {
            get
            {
                PackageInfo package = PackageInfo.FindForAssembly(typeof(RoadToolsPackagePaths).Assembly);
                return package != null ? package.resolvedPath : Path.Combine(Application.dataPath, "Road Tools");
            }
        }

        internal static string PackageRoot
        {
            get
            {
                PackageInfo package = PackageInfo.FindForAssembly(typeof(RoadToolsPackagePaths).Assembly);
                return package != null ? package.assetPath : "Packages/" + PackageName;
            }
        }

        internal static string DefaultAssetPath(string relativePath) =>
            PackageRoot + "/Scripts/Defaults/" + relativePath.TrimStart('/');

        internal static T LoadDefault<T>(string relativePath) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(DefaultAssetPath(relativePath));
            if (asset != null) return asset;
            // The development checkout keeps authored defaults in Samples instead of Scripts/Defaults.
            if (PackageInfo.FindForAssembly(typeof(RoadToolsPackagePaths).Assembly) == null)
                return AssetDatabase.LoadAssetAtPath<T>("Assets/Road Tools/Samples/" + relativePath.TrimStart('/'));
            return null;
        }

        internal static bool IsPackageAsset(UnityEngine.Object asset) => asset != null &&
            AssetDatabase.GetAssetPath(asset).StartsWith("Packages/", StringComparison.Ordinal);
    }
}
