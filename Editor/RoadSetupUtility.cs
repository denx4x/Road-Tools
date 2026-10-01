using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadSetupUtility
    {
        private const string FallbackFolder = RoadToolsPackagePaths.GeneratedRoot + "/Profiles";
        private const string FallbackProfilePath = FallbackFolder + "/Default Road Profile.asset";
        private const string FallbackMaterialPath = FallbackFolder + "/Default Road Asphalt.mat";

        internal static SplineRoad EnsureRoad(GameObject target, RoadProfile requestedProfile = null, Terrain targetTerrain = null)
        {
            if (target == null || EditorUtility.IsPersistent(target) || !target.scene.IsValid())
                return null;

            EnsureComponent<SplineContainer>(target);
            EnsureComponent<RoadMeshGenerator>(target);
            EnsureComponent<RoadColliderGenerator>(target);
            EnsureComponent<PropLayerManager>(target);
            SplineRoad road = EnsureComponent<SplineRoad>(target);
            Undo.RecordObject(road, "Set Up Road Tools");
            RoadProfile selectedProfile = road.Profile != null ? road.Profile :
                requestedProfile != null ? requestedProfile : ResolveDefaultProfile();
            if (road.Profile == null || RoadToolsPackagePaths.IsPackageAsset(selectedProfile))
                road.SetProfile(CreateEditableProfile(selectedProfile));
            road.SetLiveUpdates(true);
            RoadLiveUpdateCoordinator.EnsureTerrainRegistration(road, targetTerrain);
            road.RequestRebuild();
            EditorUtility.SetDirty(road);
            EditorSceneManager.MarkSceneDirty(target.scene);
            return road;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(target);
        }

        internal static RoadProfile ResolveDefaultProfile()
        {
            RoadProfile profile = RoadToolsPackagePaths.LoadDefault<RoadProfile>("Profiles/Two Lane Asphalt.asset");
            if (profile != null)
                return profile;
            profile = AssetDatabase.LoadAssetAtPath<RoadProfile>(FallbackProfilePath);
            if (profile != null)
                return profile;

            EnsureAssetFolder(FallbackFolder);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(FallbackMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    material = new Material(shader) { name = "Default Road Asphalt", color = new Color(0.16f, 0.17f, 0.18f) };
                    if (material.HasProperty("_Smoothness"))
                        material.SetFloat("_Smoothness", 0.15f);
                    AssetDatabase.CreateAsset(material, FallbackMaterialPath);
                }
            }
            profile = ScriptableObject.CreateInstance<RoadProfile>();
            profile.name = "Default Road Profile";
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("material").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(profile, FallbackProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        internal static RoadProfile CreateEditableProfile(RoadProfile profile)
        {
            if (profile == null || !RoadToolsPackagePaths.IsPackageAsset(profile))
                return profile;

            EnsureAssetFolder(FallbackFolder);
            RoadProfile editable = Object.Instantiate(profile);
            editable.name = profile.name;
            string path = AssetDatabase.GenerateUniqueAssetPath(
                FallbackFolder + "/" + profile.name + ".asset");
            AssetDatabase.CreateAsset(editable, path);
            AssetDatabase.SaveAssets();
            return editable;
        }

        internal static void EnsureAssetFolder(string folder)
        {
            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
