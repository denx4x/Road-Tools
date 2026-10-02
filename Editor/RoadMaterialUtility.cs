using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadMaterialUtility
    {
        internal const string MaterialUndoName = "Change Road Material";

        internal static void Set(SplineRoad road, Material material)
        {
            if (road == null || road.MaterialOverride == material) return;

            Transform root = road.transform.Find("Generated Road Mesh");
            MeshRenderer[] renderers = root != null
                ? root.GetComponentsInChildren<MeshRenderer>(true)
                : Array.Empty<MeshRenderer>();
            var objects = new UnityEngine.Object[renderers.Length + 1];
            objects[0] = road;
            for (int index = 0; index < renderers.Length; index++) objects[index + 1] = renderers[index];
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(MaterialUndoName);
            Undo.RecordObjects(objects, MaterialUndoName);
            road.SetMaterialOverride(material);
            EditorUtility.SetDirty(road);
            PrefabUtility.RecordPrefabInstancePropertyModifications(road);
            foreach (MeshRenderer renderer in renderers)
            {
                EditorUtility.SetDirty(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            if (road.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            SceneView.RepaintAll();
        }

        internal static bool IsMaterialModification(PropertyModification current)
        {
            if (current == null) return false;
            if (current.target is SplineRoad) return current.propertyPath == "materialOverride";
            if (current.target is not MeshRenderer renderer ||
                !current.propertyPath.StartsWith("m_Materials", StringComparison.Ordinal)) return false;

            SplineRoad road = renderer.GetComponentInParent<SplineRoad>();
            Transform root = road != null ? road.transform.Find("Generated Road Mesh") : null;
            return root != null && renderer.transform.IsChildOf(root);
        }
    }
}
