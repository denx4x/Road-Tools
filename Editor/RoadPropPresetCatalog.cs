using UnityEditor;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal enum RoadPropPresetKind
    {
        Fence,
        StreetLights,
        FenceAndLights
    }

    internal static class RoadPropPresetCatalog
    {
        internal static GameObject FindStreetLightPrefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(RoadToolsPackagePaths.GeneratedRoot + "/Prefabs/Street Lamp.prefab") ??
            RoadToolsPackagePaths.LoadDefault<GameObject>("Prefabs/Street Lamp.prefab") ??
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Road Tools/Samples/Prefabs/Street Lamp.prefab");

        internal static string Label(RoadPropPresetKind kind) => kind switch
        {
            RoadPropPresetKind.Fence => "Add Fence",
            RoadPropPresetKind.StreetLights => "Add Street Lights",
            _ => "Fence + Street Lights"
        };

        internal static string Description(RoadPropPresetKind kind) => kind switch
        {
            RoadPropPresetKind.Fence => "Smooth guardrails on both sides",
            RoadPropPresetKind.StreetLights => "Lamps every 9 m on both sides",
            _ => "Add both roadside layers in one click"
        };

        internal static Texture Preview(GameObject prefab)
        {
            if (prefab == null) return EditorGUIUtility.IconContent("Prefab Icon").image;
            return AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);
        }
    }
}
