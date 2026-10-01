using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    public static class RoadToolsTestSceneBuilder
    {
        private const string GeneratedFolder = RoadToolsPackagePaths.GeneratedRoot;
        private const string ScenePath = GeneratedFolder + "/Scenes/Road Tools Test.unity";
        private const string GeneratedProfilePath = GeneratedFolder + "/Profiles/Two Lane Asphalt.asset";
        private const string GeneratedMaterialPath = GeneratedFolder + "/Materials/Road Asphalt.mat";
        private const string GeneratedLampPrefabPath = GeneratedFolder + "/Prefabs/Street Lamp.prefab";

        [MenuItem("Tools/Road Tools/Create Test Scene")]
        public static void Build()
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Create Road Tools Test Scene",
                "Where should the road, terrain, and intersection test setup be created?",
                "New Scene",
                "Current Scene",
                "Cancel");
            if (choice == 0)
                BuildNewScene();
            else if (choice == 1)
                BuildInCurrentScene();
        }

        public static void BuildNewScene()
        {
            EnsureGeneratedFolders();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            string uniqueScenePath = AssetDatabase.GenerateUniqueAssetPath(ScenePath);
            EditorSceneManager.SaveScene(scene, uniqueScenePath);
            SplineRoad road = BuildInScene(scene);
            if (road == null)
                return;

            EditorSceneManager.SaveScene(scene, uniqueScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Road Tools test scene created at {uniqueScenePath}.", road);
        }

        public static void BuildInCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("Road Tools needs an open active scene.");
                return;
            }

            SplineRoad road = BuildInScene(scene);
            if (road != null)
                Debug.Log($"Road Tools test setup is ready in " +
                    $"{(string.IsNullOrWhiteSpace(scene.name) ? "the current unsaved scene" : scene.name)}. " +
                    "Save this scene to keep changes.", road);
        }

        private static SplineRoad BuildInScene(Scene scene)
        {
            EnsureGeneratedFolders();
            Material material = GetOrCreateMaterial();
            RoadProfile profile = GetOrCreateProfile(material);
            GameObject lampPrefab = GetOrCreateLampPrefab();
            SetupEnvironment(scene);
            SplineRoad road = FindRoad(scene) ?? CreateSampleRoad(profile, lampPrefab);
            RoadToolsTerrainSampleBuilder.AddToScene(scene, road);
            if (!HasIntersection(scene))
                CreateIntersectionPreview();
            road.Rebuild();
            Selection.activeGameObject = road.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorSceneManager.MarkSceneDirty(scene);
            return road;
        }

        private static SplineRoad FindRoad(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SplineRoad road = root.GetComponentInChildren<SplineRoad>(true);
                if (road != null)
                    return road;
            }
            return null;
        }

        private static bool HasIntersection(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<RoadIntersection>(true) != null ||
                    root.name == "Intersection Socket Preview")
                    return true;
            }
            return false;
        }

        private static SplineRoad CreateSampleRoad(RoadProfile profile, GameObject lampPrefab)
        {
            var roadObject = new GameObject("Demo Spline Road");
            var container = roadObject.AddComponent<SplineContainer>();
            container.Spline.Clear();
            container.Spline.Add(new BezierKnot(new Unity.Mathematics.float3(-18f, 0f, -12f)));
            container.Spline.Add(new BezierKnot(new Unity.Mathematics.float3(-8f, 0f, 0f)));
            container.Spline.Add(new BezierKnot(new Unity.Mathematics.float3(8f, 0f, 6f)));
            container.Spline.Add(new BezierKnot(new Unity.Mathematics.float3(20f, 0f, 18f)));
            for (int i = 0; i < container.Spline.Count; i++)
                container.Spline.SetTangentMode(i, TangentMode.AutoSmooth);

            roadObject.AddComponent<RoadMeshGenerator>();
            PropLayerManager props = roadObject.AddComponent<PropLayerManager>();
            var road = roadObject.AddComponent<SplineRoad>();
            road.SetProfile(profile);

            var serializedProps = new SerializedObject(props);
            SerializedProperty layers = serializedProps.FindProperty("layers");
            layers.arraySize = 1;
            SerializedProperty layer = layers.GetArrayElementAtIndex(0);
            layer.FindPropertyRelative("name").stringValue = "Street Lamps";
            layer.FindPropertyRelative("prefab").objectReferenceValue = lampPrefab;
            layer.FindPropertyRelative("side").enumValueIndex = (int)PropSide.Both;
            layer.FindPropertyRelative("spacing").floatValue = 9f;
            layer.FindPropertyRelative("lateralOffset").floatValue = 1.25f;
            layer.FindPropertyRelative("randomRotation").vector3Value = new Vector3(0f, 4f, 0f);
            layer.FindPropertyRelative("randomScale").vector2Value = new Vector2(0.95f, 1.05f);
            serializedProps.ApplyModifiedPropertiesWithoutUndo();
            return road;
        }

        private static void SetupEnvironment(Scene scene)
        {
            bool hasCamera = false;
            bool hasLight = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                hasCamera |= root.GetComponentInChildren<Camera>(true) != null;
                hasLight |= root.GetComponentInChildren<Light>(true) != null;
            }

            if (!hasCamera)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 34f, -38f), Quaternion.Euler(35f, 0f, 0f));
                camera.clearFlags = CameraClearFlags.Skybox;
            }

            if (!hasLight)
            {
                var lightObject = new GameObject("Directional Light");
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            }
        }

        private static void CreateIntersectionPreview()
        {
            GameObject prefab = RoadToolsPackagePaths.LoadDefault<GameObject>("Prefabs/4-Way Socket Hub.prefab") ??
                AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedFolder + "/Prefabs/4-Way Socket Hub.prefab");
            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetPositionAndRotation(new Vector3(20f, 0f, 22f), Quaternion.identity);
                return;
            }

            var root = new GameObject("Intersection Socket Preview");
            root.transform.position = new Vector3(20f, 0f, 18f);
            for (int i = 0; i < 3; i++)
            {
                var socket = new GameObject($"Road Socket {i + 1}");
                socket.transform.SetParent(root.transform, false);
                socket.transform.rotation = Quaternion.Euler(0f, i * 120f, 0f);
                socket.transform.position = root.transform.position + socket.transform.forward * 4f;
                socket.AddComponent<RoadSocket>();
            }
        }

        private static Material GetOrCreateMaterial()
        {
            Material existing = RoadToolsPackagePaths.LoadDefault<Material>("Materials/Road Asphalt Marked.mat") ??
                AssetDatabase.LoadAssetAtPath<Material>(GeneratedMaterialPath);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Road Asphalt" };
            material.color = new Color(0.16f, 0.17f, 0.18f, 1f);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, GeneratedMaterialPath);
            return material;
        }

        private static RoadProfile GetOrCreateProfile(Material material)
        {
            RoadProfile existing = RoadToolsPackagePaths.LoadDefault<RoadProfile>("Profiles/Two Lane Asphalt.asset") ??
                AssetDatabase.LoadAssetAtPath<RoadProfile>(GeneratedProfilePath);
            if (existing != null)
                return RoadSetupUtility.CreateEditableProfile(existing);

            var profile = ScriptableObject.CreateInstance<RoadProfile>();
            var serializedProfile = new SerializedObject(profile);
            serializedProfile.FindProperty("material").objectReferenceValue = material;
            serializedProfile.FindProperty("width").floatValue = 7f;
            serializedProfile.FindProperty("sampleSpacing").floatValue = 0.75f;
            serializedProfile.FindProperty("chunkLength").floatValue = 20f;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(profile, GeneratedProfilePath);
            return profile;
        }

        private static GameObject GetOrCreateLampPrefab()
        {
            GameObject existing = RoadToolsPackagePaths.LoadDefault<GameObject>("Prefabs/Street Lamp.prefab") ??
                AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedLampPrefabPath);
            if (existing != null)
                return existing;

            var root = new GameObject("Street Lamp");
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            pole.transform.localScale = new Vector3(0.08f, 2.5f, 0.08f);
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Lamp Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 5f, 0.35f);
            head.transform.localScale = new Vector3(0.35f, 0.15f, 0.7f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, GeneratedLampPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void EnsureGeneratedFolders()
        {
            RoadSetupUtility.EnsureAssetFolder(GeneratedFolder + "/Scenes");
            RoadSetupUtility.EnsureAssetFolder(GeneratedFolder + "/Profiles");
            RoadSetupUtility.EnsureAssetFolder(GeneratedFolder + "/Materials");
            RoadSetupUtility.EnsureAssetFolder(GeneratedFolder + "/Prefabs");
        }
    }
}
