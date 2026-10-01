using UnityEditor;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadToolsFenceAssetBuilder
    {
        private const string GeneratedFolder = RoadToolsPackagePaths.GeneratedRoot;
        private const string MaterialPath = GeneratedFolder + "/Materials/Galvanized Fence.mat";
        private const string PrefabPath = GeneratedFolder + "/Prefabs/Roadside Fence.prefab";

        internal static GameObject GetOrCreatePrefab()
        {
            GameObject existing = RoadToolsPackagePaths.LoadDefault<GameObject>("Prefabs/Roadside Fence.prefab") ??
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
                return existing;

            RoadSetupUtility.EnsureAssetFolder(GeneratedFolder + "/Materials");
            RoadSetupUtility.EnsureAssetFolder(GeneratedFolder + "/Prefabs");

            Texture2D texture = RoadToolsPackagePaths.LoadDefault<Texture2D>("Textures/Weathered Galvanized Steel.png");
            if (texture == null)
            {
                Debug.LogError($"Fence texture missing: {RoadToolsPackagePaths.DefaultAssetPath("Textures/Weathered Galvanized Steel.png")}");
                return null;
            }

            Material steel = RoadToolsPackagePaths.LoadDefault<Material>("Materials/Galvanized Fence.mat") ??
                AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (steel == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("URP Lit shader is required for the fence material.");
                    return null;
                }

                steel = new Material(shader) { name = "Galvanized Fence", enableInstancing = true };
                steel.SetTexture("_BaseMap", texture);
                steel.SetColor("_BaseColor", new Color(0.8f, 0.82f, 0.82f));
                steel.SetFloat("_Metallic", 0.65f);
                steel.SetFloat("_Smoothness", 0.35f);
                AssetDatabase.CreateAsset(steel, MaterialPath);
            }

            GameObject root = new GameObject("Roadside Fence");
            try
            {
                // The spline repeater advances three meters per instance along local Z.
                AddBox(root.transform, "Steel Post", new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 1f, 0.12f), steel);
                AddBox(root.transform, "Rail Back", new Vector3(0f, 0.78f, 0f), new Vector3(0.08f, 0.32f, 3.35f), steel);
                AddBox(root.transform, "Upper Rolled Edge", new Vector3(0.075f, 0.95f, 0f), new Vector3(0.08f, 0.055f, 3.35f), steel);
                AddBox(root.transform, "Lower Rolled Edge", new Vector3(0.075f, 0.61f, 0f), new Vector3(0.08f, 0.055f, 3.35f), steel);
                AddBox(root.transform, "Center Rib", new Vector3(0.065f, 0.78f, 0f), new Vector3(0.06f, 0.055f, 3.35f), steel);
                AddBox(root.transform, "Post Bracket", new Vector3(0.09f, 0.78f, 0f), new Vector3(0.1f, 0.25f, 0.14f), steel);
                for (int i = -1; i <= 1; i += 2)
                    AddBox(root.transform, "Rail Bolt", new Vector3(0.15f, 0.78f, i * 0.055f),
                        new Vector3(0.025f, 0.035f, 0.035f), steel);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
