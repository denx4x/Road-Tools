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

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoDestination + "/Scenes/Road Tools Demo.unity") != null)
            {
                RoadToolsWorkspace.PingAsset(DemoDestination);
                message = "Existing demo selected at " + DemoDestination + ". Your assets were kept.";
                return true;
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
            string previousAsset = FindExistingSampleAsset(sample.resolvedPath, out bool existingScene);
            if (!string.IsNullOrEmpty(previousAsset))
            {
                RoadToolsWorkspace.PingAsset(previousAsset);
                message = existingScene
                    ? "Existing demo selected at " + previousAsset + ". Your assets were kept."
                    : "Some demo assets already exist at " + previousAsset + ". " +
                      "A duplicate import was skipped to keep existing asset references intact.";
                if (!existingScene) type = MessageType.Warning;
                return existingScene;
            }

            string importedPath = ToAssetPath(sample.importPath);
            if (sample.isImported || (!string.IsNullOrEmpty(importedPath) &&
                                      AssetDatabase.IsValidFolder(importedPath)))
            {
                if (!string.IsNullOrEmpty(importedPath)) RoadToolsWorkspace.PingAsset(importedPath);
                message = "The demo is already imported. Its existing assets were kept.";
                return true;
            }

            if (!sample.Import(Sample.ImportOptions.HideImportWindow))
            {
                message = "Unity could not import the demo. Existing imports were kept.";
                type = MessageType.Error;
                return false;
            }

            AssetDatabase.Refresh();
            importedPath = ToAssetPath(sample.importPath);
            if (string.IsNullOrEmpty(importedPath) || !AssetDatabase.IsValidFolder(importedPath))
            {
                message = "Unity imported the demo but its folder is not available yet. " +
                          "Check the Samples section in Package Manager.";
                type = MessageType.Warning;
                return false;
            }

            // Keep Unity's canonical location so Package Manager can track and reimport this sample.
            RoadToolsWorkspace.PingAsset(importedPath);
            message = "Demo imported at " + importedPath + ". Open its scene when you are ready.";
            return true;
        }

        private static PackageInfo FindPackage()
        {
            PackageInfo owner = PackageInfo.FindForAssembly(typeof(RoadToolsSampleImporter).Assembly);
            if (owner != null && owner.name == RoadToolsPackagePaths.PackageName) return owner;
            foreach (PackageInfo package in PackageInfo.GetAllRegisteredPackages())
                if (package.name == RoadToolsPackagePaths.PackageName) return package;
            return null;
        }

        private static string FindExistingSampleAsset(string sourcePath, out bool existingScene)
        {
            existingScene = false;
            if (!Directory.Exists(sourcePath)) return null;
            string[] metadata = Directory.GetFiles(sourcePath, "*.meta", SearchOption.AllDirectories);
            Array.Sort(metadata, (left, right) =>
                IsSceneMeta(right).CompareTo(IsSceneMeta(left)));
            foreach (string metaPath in metadata)
            {
                foreach (string line in File.ReadLines(metaPath))
                {
                    if (!line.StartsWith("guid: ", StringComparison.Ordinal)) continue;
                    string existingPath = AssetDatabase.GUIDToAssetPath(line.Substring(6).Trim());
                    bool sceneCandidate = IsSceneMeta(metaPath);
                    if (existingPath.StartsWith("Assets/", StringComparison.Ordinal) &&
                        ExistingAssetIsPresent(existingPath, sceneCandidate))
                    {
                        existingScene = sceneCandidate;
                        return existingPath;
                    }
                    break;
                }
            }
            return null;
        }

        private static bool ExistingAssetIsPresent(string assetPath, bool scene)
        {
            // GUIDToAssetPath can retain mappings for assets that were deleted or moved outside Unity.
            string fullPath = RoadToolsWorkspace.ToFullPath(assetPath);
            if (scene)
                return File.Exists(fullPath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPath) != null;
            return File.Exists(fullPath) || Directory.Exists(fullPath);
        }

        private static bool IsSceneMeta(string path) =>
            path.EndsWith(".unity.meta", StringComparison.OrdinalIgnoreCase);

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
