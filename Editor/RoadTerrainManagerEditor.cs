using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    [CustomEditor(typeof(RoadTerrainManager))]
    public sealed class RoadTerrainManagerEditor : UnityEditor.Editor
    {
        private string status;
        private MessageType statusType = MessageType.Info;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            RoadTerrainManager manager = (RoadTerrainManager)target;
            Terrain terrain = manager.GetComponent<Terrain>();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Capture the unmodified terrain once. Rebuild restores that base, applies listed roads, then refreshes their editable road meshes and props.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(manager.BaseSnapshot != null || terrain == null || terrain.terrainData == null))
            {
                if (GUILayout.Button("Capture Terrain Base Snapshot"))
                {
                    status = $"Captured terrain base at {Capture(manager, terrain)}.";
                    statusType = MessageType.Info;
                }
            }

            using (new EditorGUI.DisabledScope(manager.BaseSnapshot == null))
            {
                if (GUILayout.Button("Rebuild Terrain From Base"))
                {
                    Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Rebuild Road Terrain");
                    status = manager.RebuildTerrain();
                    statusType = manager.LastRebuildSucceeded ? MessageType.Info : MessageType.Error;
                    EditorUtility.SetDirty(terrain.terrainData);
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }
            }

            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, statusType);
        }

        internal static string Capture(RoadTerrainManager manager, Terrain terrain, bool recordUndo = true)
        {
            string scenePath = manager.gameObject.scene.path;
            string sceneFolder = string.IsNullOrEmpty(scenePath)
                ? "Assets"
                : Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(sceneFolder) || !AssetDatabase.IsValidFolder(sceneFolder))
                sceneFolder = "Assets";
            string folder = $"{sceneFolder}/Road Tools Terrain";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(sceneFolder, "Road Tools Terrain");

            RoadTerrainBaseAsset snapshot = CreateInstance<RoadTerrainBaseAsset>();
            snapshot.CaptureFrom(terrain.terrainData);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{terrain.name} Base.asset");
            AssetDatabase.CreateAsset(snapshot, path);
            AssetDatabase.SaveAssets();
            if (recordUndo) Undo.RecordObject(manager, "Capture Terrain Base Snapshot");
            manager.SetBaseSnapshot(snapshot);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            return path;
        }
    }
}
