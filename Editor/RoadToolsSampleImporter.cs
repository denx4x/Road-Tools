using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadToolsSampleImporter
    {
        internal const string DemoDestination = RoadToolsWorkspace.SamplesRoot + "/Demo";

        [MenuItem("Tools/Road Tools/Import Demo Sample", false, 52)]
        private static void ImportFromMenu()
        {
            ImportDemo(out string message, out MessageType type);
            if (type == MessageType.Error) Debug.LogError(message);
            else if (type == MessageType.Warning) Debug.LogWarning(message);
            else Debug.Log(message);
        }

        internal static bool ImportDemo(out string message, out MessageType type)
        {
            type = MessageType.Info;
            if (!RoadToolsWorkspace.EnsureProjectFolders())
            {
                message = "Wait for Unity to finish importing, then import the demo again.";
                type = MessageType.Warning;
                return false;
            }

            try
            {
                PackageInfo package = FindPackage();
                if (package == null)
                {
                    message = "Install Road Tools through Package Manager to import its demo. " +
                              "You can also create a test setup in the current scene.";
                    type = MessageType.Warning;
                    return false;
                }

                foreach (Sample sample in Sample.FindByPackage(package.name, package.version))
                {
                    if (!string.Equals(sample.displayName, "Demo", StringComparison.Ordinal)) continue;
                    return ImportSample(sample, out message, out type);
                }

                message = "This Road Tools package does not include the Demo sample.";
                type = MessageType.Warning;
                return false;
            }
            catch (Exception exception) when (exception is IOException ||
                                              exception is UnauthorizedAccessException ||
                                              exception is ArgumentException)
            {
                message = "Road Tools could not import the demo: " + exception.Message;
                type = MessageType.Error;
                return false;
            }
        }

        private static bool ImportSample(Sample sample, out string message, out MessageType type)
        {
            type = MessageType.Info;
            string importedPath = ToAssetPath(sample.importPath);
            if (string.IsNullOrEmpty(importedPath))
                throw new ArgumentException("Unity returned an invalid sample import path.");
            bool success = RoadToolsSampleContent.Repair(sample.resolvedPath, importedPath, out message);
            RoadToolsWorkspace.PingAsset(importedPath);
            return success;
        }

        private static PackageInfo FindPackage()
        {
            PackageInfo owner = PackageInfo.FindForAssembly(typeof(RoadToolsSampleImporter).Assembly);
            if (owner != null && owner.name == RoadToolsPackagePaths.PackageName) return owner;
            foreach (PackageInfo package in PackageInfo.GetAllRegisteredPackages())
                if (package.name == RoadToolsPackagePaths.PackageName) return package;
            return null;
        }

        private static string ToAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            path = path.Replace('\\', '/');
            if (path.StartsWith("Assets/", StringComparison.Ordinal)) return path;
            string assetsRoot = Application.dataPath.Replace('\\', '/').TrimEnd('/') + "/";
            return path.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase)
                ? "Assets/" + path.Substring(assetsRoot.Length)
                : null;
        }
    }
}
