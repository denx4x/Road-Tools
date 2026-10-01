using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dyma.SplineLevelToolkit.Editor
{
    public static class RoadToolsTerrainSampleBuilder
    {
        private const string Folder = "Assets/Road Tools/Generated/Terrain";
        private const string TexturePackFolder =
            "Assets/ObjectiveEnvironment_Assets/Realistic Terrain Textures Lite/Terrain Layers";

        public static Terrain AddToScene(Scene scene, SplineRoad road)
        {
            if (!scene.IsValid() || road == null || road.Profile == null)
                return null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out Terrain existing))
                {
                    ConfigureExistingTerrain(scene, existing, road);
                    return existing;
                }
            }

            EnsureFolder();
            var data = new TerrainData
            {
                heightmapResolution = 129,
                alphamapResolution = 64,
                size = new Vector3(100f, 12f, 100f)
            };
            data.terrainLayers = LoadTerrainLayers();
            var heights = new float[129, 129];
            for (int z = 0; z < 129; z++)
            for (int x = 0; x < 129; x++)
            {
                float worldX = -50f + x / 128f * 100f;
                float worldZ = -50f + z / 128f * 100f;
                float worldHeight = -0.3f +
                    0.45f * Mathf.Sin(worldX * 0.08f) * Mathf.Cos(worldZ * 0.07f) +
                    0.35f * Mathf.Sin((worldX + worldZ) * 0.045f);
                heights[z, x] = Mathf.Clamp01((worldHeight + 1f) / data.size.y);
            }
            data.SetHeights(0, 0, heights);

            string terrainPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{Folder}/{SceneLabel(scene)} Terrain.asset");
            AssetDatabase.CreateAsset(data, terrainPath);
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Road Tools Test Terrain";
            SceneManager.MoveGameObjectToScene(terrainObject, scene);
            terrainObject.transform.position = new Vector3(-50f, -1f, -50f);
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;

            RoadTerrainBaseAsset snapshot = ScriptableObject.CreateInstance<RoadTerrainBaseAsset>();
            snapshot.CaptureFrom(data);
            string snapshotPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{Folder}/{SceneLabel(scene)} Terrain Base.asset");
            AssetDatabase.CreateAsset(snapshot, snapshotPath);

            RoadProfile terrainProfile = CreateTerrainProfile(scene, road.Profile, data, true);

            RoadTerrainManager manager = terrainObject.AddComponent<RoadTerrainManager>();
            manager.SetBaseSnapshot(snapshot);
            road.SetProfile(terrainProfile);
            manager.AddRoad(road);
            manager.RebuildTerrain();

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(road);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            return terrain;
        }

        private static void ConfigureExistingTerrain(Scene scene, Terrain terrain, SplineRoad road)
        {
            if (terrain.terrainData == null)
                return;

            EnsureFolder();
            RoadTerrainManager manager = terrain.GetComponent<RoadTerrainManager>();
            if (manager == null)
                manager = terrain.gameObject.AddComponent<RoadTerrainManager>();
            if (manager.BaseSnapshot == null)
            {
                RoadTerrainBaseAsset snapshot = ScriptableObject.CreateInstance<RoadTerrainBaseAsset>();
                snapshot.CaptureFrom(terrain.terrainData);
                string snapshotPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{Folder}/{SceneLabel(scene)} Terrain Base.asset");
                AssetDatabase.CreateAsset(snapshot, snapshotPath);
                manager.SetBaseSnapshot(snapshot);
            }

            if (!road.Profile.ConformRoadToTerrain || !road.Profile.DeformTerrain)
                road.SetProfile(CreateTerrainProfile(scene, road.Profile, terrain.terrainData, false));
            manager.AddRoad(road);
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Build Road Tools Terrain Test");
            manager.RebuildTerrain();
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(terrain.terrainData);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        private static RoadProfile CreateTerrainProfile(Scene scene, RoadProfile baseProfile, TerrainData data,
            bool addSampleBands)
        {
            RoadProfile profile = Object.Instantiate(baseProfile);
            profile.name = "Two Lane Asphalt Terrain";
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("conformRoadToTerrain").boolValue = true;
            serialized.FindProperty("deformTerrain").boolValue = true;
            serialized.FindProperty("terrainLayerIndex").intValue = Mathf.Max(0, Mathf.Min(1, data.alphamapLayers - 1));
            if (addSampleBands && data.terrainLayers.Length >= 2)
            {
                TerrainLayer third = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
                    $"{TexturePackFolder}/Ground002.terrainlayer");
                TerrainLayer[] sampleLayers = third != null
                    ? new[] { data.terrainLayers[0], data.terrainLayers[1], third }
                    : new[] { data.terrainLayers[0], data.terrainLayers[1] };
                float[] starts = { 0f, 4f, 8f };
                float[] widths = { 4f, 4f, 6f };
                SerializedProperty bands = serialized.FindProperty("terrainPaintBands");
                bands.arraySize = sampleLayers.Length;
                for (int i = 0; i < sampleLayers.Length; i++)
                {
                    SerializedProperty band = bands.GetArrayElementAtIndex(i);
                    band.FindPropertyRelative("layer").objectReferenceValue = sampleLayers[i];
                    band.FindPropertyRelative("startDistance").floatValue = starts[i];
                    band.FindPropertyRelative("width").floatValue = widths[i];
                    band.FindPropertyRelative("blendDistance").floatValue = 2f;
                }
                serialized.FindProperty("paintTerrainLayer").boolValue = false;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string profilePath = AssetDatabase.GenerateUniqueAssetPath(
                $"{Folder}/{SceneLabel(scene)} Road Profile.asset");
            AssetDatabase.CreateAsset(profile, profilePath);
            return profile;
        }

        private static TerrainLayer[] LoadTerrainLayers()
        {
            TerrainLayer grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
                $"{TexturePackFolder}/Ground006.terrainlayer");
            TerrainLayer ground = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
                $"{TexturePackFolder}/Ground003.terrainlayer");
            if (grass == null)
                grass = CreateFallbackLayer("Grass", new Color(0.36f, 0.5f, 0.3f));
            if (ground == null)
                ground = CreateFallbackLayer("Ground", new Color(0.48f, 0.39f, 0.3f));
            return new[] { grass, ground };
        }

        private static TerrainLayer CreateFallbackLayer(string name, Color color)
        {
            var texture = new Texture2D(4, 4) { name = $"Road Tools {name} Texture" };
            var colors = new Color[16];
            for (int index = 0; index < colors.Length; index++)
                colors[index] = color;
            texture.SetPixels(colors);
            texture.Apply();
            string texturePath = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{texture.name}.asset");
            AssetDatabase.CreateAsset(texture, texturePath);
            var layer = new TerrainLayer { name = $"Road Tools {name}", diffuseTexture = texture };
            string layerPath = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{layer.name}.terrainlayer");
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }

        private static void EnsureFolder()
        {
            RoadSetupUtility.EnsureAssetFolder(Folder);
        }

        private static string SceneLabel(Scene scene) =>
            string.IsNullOrWhiteSpace(scene.name) ? "Current Scene" : scene.name;
    }
}
