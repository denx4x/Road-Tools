using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    [InitializeOnLoad]
    internal static class RoadToolsWorkspace
    {
        internal const string ProjectRoot = "Assets/Road Tools";
        internal const string SamplesRoot = ProjectRoot + "/Samples";
        internal const string DocumentationRoot = ProjectRoot + "/Documentation";
        internal const string DevelopmentRoot = ProjectRoot + "/Development";
        internal const string QuickStartPath = DocumentationRoot + "/QuickStart.md";

        private const int StartupAttempts = 120;
        private static int remainingAttempts;
        private static double nextAttempt;
        private static bool scheduled;
        private static string lastFailure;

        static RoadToolsWorkspace() => ScheduleEnsure();

        internal static bool EnsureProjectFolders()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ScheduleEnsure();
                return false;
            }

            try
            {
                EnsureFolder(ProjectRoot);
                EnsureFolder(SamplesRoot);
                EnsureFolder(DocumentationRoot);
                EnsureFolder(RoadToolsPackagePaths.GeneratedRoot);
                EnsureFolder(DevelopmentRoot);

                string template = Path.Combine(RoadToolsPackagePaths.SourceRoot,
                    "Documentation~", "QuickStart.md");
                WriteNewAsset(QuickStartPath, File.Exists(template)
                    ? File.ReadAllText(template)
                    : "# Road Tools Quick Start\n\nOpen Tools > Road Tools > Open Window. " +
                      "Create a road in the Road tab, add fence or lamp presets in Props, " +
                      "and connect a terrain in Terrain. Import the optional demo from Test Scene.\n");
                WriteNewAsset(SamplesRoot + "/README.md", SamplesReadme);
                lastFailure = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException ||
                                              exception is UnauthorizedAccessException)
            {
                if (lastFailure != exception.Message)
                    Debug.LogWarning("Road Tools could not create its project folders: " + exception.Message);
                lastFailure = exception.Message;
                return false;
            }
        }

        [MenuItem("Tools/Road Tools/Create Project Folders", false, 50)]
        private static void CreateProjectFolders()
        {
            if (EnsureProjectFolders()) OpenProjectFolder();
        }

        [MenuItem("Tools/Road Tools/Open Project Folder", false, 51)]
        internal static void OpenProjectFolder()
        {
            if (EnsureProjectFolders()) PingAsset(ProjectRoot);
        }

        [MenuItem("Tools/Road Tools/Open Quick Start", false, 53)]
        internal static void OpenDocumentation()
        {
            if (!EnsureProjectFolders()) return;
            UnityEngine.Object guide = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(QuickStartPath);
            if (guide != null) AssetDatabase.OpenAsset(guide);
        }

        internal static void PingAsset(string assetPath)
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        internal static string ToFullPath(string assetPath) =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, assetPath);

        private static void ScheduleEnsure()
        {
            if (scheduled) return;
            scheduled = true;
            remainingAttempts = StartupAttempts;
            nextAttempt = EditorApplication.timeSinceStartup;
            EditorApplication.update += EnsureWhenReady;
        }

        private static void EnsureWhenReady()
        {
            if (EditorApplication.timeSinceStartup < nextAttempt) return;
            nextAttempt = EditorApplication.timeSinceStartup + 1d;
            remainingAttempts--;
            if (EnsureProjectFolders() || remainingAttempts <= 0)
            {
                EditorApplication.update -= EnsureWhenReady;
                scheduled = false;
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            int separator = assetPath.LastIndexOf('/');
            string parent = assetPath.Substring(0, separator);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, assetPath.Substring(separator + 1))))
                throw new IOException("Unable to create " + assetPath + ".");
        }

        private static void WriteNewAsset(string assetPath, string content)
        {
            string filePath = ToFullPath(assetPath);
            if (File.Exists(filePath)) return;
            using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(content);
            AssetDatabase.ImportAsset(assetPath);
        }

        private const string SamplesReadme =
            "# Road Tools Samples\n\n" +
            "Open **Tools > Road Tools > Open Window**, select **Test Scene**, " +
            "and press **Import Demo Sample** to import the optional terrain and road demo.\n\n" +
            "Unity imports the demo into `Assets/Samples/Road Tools/<version>/Demo` " +
            "so Package Manager can track it. The import button selects the actual demo folder. " +
            "If the demo was imported before, Road Tools keeps that copy and selects it. " +
            "Importing does not open or replace your current scene.\n\n" +
            "See `Assets/Road Tools/Documentation/QuickStart.md` for the usage guide.\n";
    }
}
