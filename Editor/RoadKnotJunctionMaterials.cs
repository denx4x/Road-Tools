using UnityEditor;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    // Imported splines retain their indices within B; only the container offset changes.
    internal static class RoadKnotJunctionMaterials
    {
        internal static void Transfer(SplineRoad source, SplineRoad destination, int splineOffset)
        {
            var from = source.GetComponent<RoadMaterialSections>();
            if (from == null || from.Sections.Count == 0) return;
            var to = destination.GetComponent<RoadMaterialSections>();
            if (to == null) to = Undo.AddComponent<RoadMaterialSections>(destination.gameObject);
            Undo.RegisterCompleteObjectUndo(to, "Join Road Knots");
            // A disabled destination must not suppress imported active sections. Preserve
            // its existing entries as disabled before enabling the component.
            if (!to.enabled)
            {
                var existing = new SerializedObject(to);
                var entries = existing.FindProperty("sections");
                for (int i = 0; i < entries.arraySize; i++)
                    entries.GetArrayElementAtIndex(i).FindPropertyRelative("enabled").boolValue = false;
                existing.ApplyModifiedPropertiesWithoutUndo();
                to.enabled = true;
            }
            int first = to.Sections.Count;
            foreach (RoadMaterialSection section in from.Sections)
                if (section != null)
                    to.Add(JsonUtility.FromJson<RoadMaterialSection>(JsonUtility.ToJson(section)));
            var serialized = new SerializedObject(to);
            var sections = serialized.FindProperty("sections");
            for (int i = first; i < sections.arraySize; i++)
            {
                var entry = sections.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("splineIndex").intValue += splineOffset;
                if (!from.enabled) entry.FindPropertyRelative("enabled").boolValue = false;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(to);
            PrefabUtility.RecordPrefabInstancePropertyModifications(to);
        }
    }
}
