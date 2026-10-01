using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    public readonly struct RoadBakeReport
    {
        public RoadBakeReport(bool succeeded, string message, string folder = null, int meshCount = 0)
        {
            Succeeded = succeeded;
            Message = message;
            Folder = folder;
            MeshCount = meshCount;
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public string Folder { get; }
        public int MeshCount { get; }
    }

    public static class RoadBakeUtility
    {
        public static RoadBakeReport Bake(SplineRoad road)
        {
            if (road == null)
                return new RoadBakeReport(false, "Select a road before baking.");
            if (road.IsBaked)
                return new RoadBakeReport(false, "This road is already baked. Resume editing to create a new bake.");

            RoadBuildReport build = road.Rebuild();
            if (!build.Succeeded)
                return new RoadBakeReport(false, build.Message);

            Transform generatedRoot = road.transform.Find("Generated Road Mesh");
            MeshFilter[] filters = generatedRoot != null
                ? generatedRoot.GetComponentsInChildren<MeshFilter>(true)
                : Array.Empty<MeshFilter>();
            if (filters.Length == 0)
                return new RoadBakeReport(false, "No generated road meshes are available to bake.");

            string scenePath = road.gameObject.scene.path;
            string sceneName = string.IsNullOrEmpty(scenePath)
                ? "Unsaved Scene"
                : Path.GetFileNameWithoutExtension(scenePath);
            string bakeRoot = $"Assets/Road Tools/Generated/Baked/{Sanitize(sceneName)}";
            RoadSetupUtility.EnsureAssetFolder(bakeRoot);

            string uniqueFolder = AssetDatabase.GenerateUniqueAssetPath($"{bakeRoot}/{Sanitize(road.name)}");
            string folderGuid = AssetDatabase.CreateFolder(bakeRoot, Path.GetFileName(uniqueFolder));
            string roadFolder = AssetDatabase.GUIDToAssetPath(folderGuid);
            if (string.IsNullOrEmpty(roadFolder))
                return new RoadBakeReport(false, "Unity could not create the bake output folder.");

            try
            {
                var bakedMeshes = new List<Mesh>(filters.Length);
                for (int index = 0; index < filters.Length; index++)
                {
                    Mesh source = filters[index].sharedMesh;
                    if (source == null)
                        throw new InvalidOperationException($"Road mesh {index + 1} is missing.");

                    Mesh copy = UnityEngine.Object.Instantiate(source);
                    copy.name = source.name;
                    string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                        $"{roadFolder}/{Sanitize(copy.name)}.asset");
                    AssetDatabase.CreateAsset(copy, assetPath);
                    bakedMeshes.Add(copy);
                }

                Undo.RecordObject(road, "Bake Road Meshes");
                for (int index = 0; index < filters.Length; index++)
                {
                    MeshFilter filter = filters[index];
                    Undo.RecordObject(filter, "Bake Road Meshes");
                    filter.sharedMesh = bakedMeshes[index];
                    EditorUtility.SetDirty(filter);
                    if (filter.TryGetComponent(out MeshCollider collider))
                    {
                        Undo.RecordObject(collider, "Bake Road Meshes");
                        collider.sharedMesh = bakedMeshes[index];
                        EditorUtility.SetDirty(collider);
                    }
                }

                road.MarkBaked();
                road.GetComponent<RoadMeshGenerator>().ReleaseGeneratedMeshes();
                EditorUtility.SetDirty(road);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                return new RoadBakeReport(
                    true,
                    $"Baked {bakedMeshes.Count} road meshes to {roadFolder}. Save the scene to keep the baked references.",
                    roadFolder,
                    bakedMeshes.Count);
            }
            catch (Exception exception)
            {
                road.ResumeEditing();
                AssetDatabase.DeleteAsset(roadFolder);
                return new RoadBakeReport(false, $"Bake failed: {exception.Message}");
            }
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "Road" : value;
        }
    }
}
