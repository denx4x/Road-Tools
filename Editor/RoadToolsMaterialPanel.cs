using UnityEditor;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadToolsMaterialPanel
    {
        internal static void Draw(SplineRoad road)
        {
            if (road == null) return;
            Material selected = road.MaterialOverride;
            DrawChoice(ref selected, road.Profile);
            if (selected != road.MaterialOverride) RoadMaterialUtility.Set(road, selected);
            EditorGUILayout.LabelField("Main material applies outside local sections. Local overrides stay unchanged.", RoadToolsWindowStyles.Body);
        }

        internal static void DrawTemplate(ref Material selected, RoadProfile profile) => DrawChoice(ref selected, profile);

        internal static void DrawChoice(ref Material selected, RoadProfile profile, bool preview = true, string defaultLabel = null)
        {
            int current = RoadMaterialPresetCatalog.Find(selected);
            string[] labels = RoadMaterialPresetCatalog.Labels;
            if (defaultLabel != null) { labels = (string[])labels.Clone(); labels[0] = defaultLabel; }
            if (current < 0)
            {
                labels = new string[labels.Length + 1];
                RoadMaterialPresetCatalog.Labels.CopyTo(labels, 0);
                if (defaultLabel != null) labels[0] = defaultLabel;
                labels[labels.Length - 1] = "Custom Material";
                current = labels.Length - 1;
            }
            int choice = EditorGUILayout.Popup("Surface Preset", current, labels);
            if (choice != current)
            {
                Material preset = RoadMaterialPresetCatalog.Load(choice);
                if (choice == 0 || preset != null) selected = preset;
            }
            selected = (Material)EditorGUILayout.ObjectField("Material Override", selected, typeof(Material), false);
            if (!preview) return;
            Material effective = selected != null ? selected : profile != null ? profile.Material : null;
            EditorGUILayout.Space(4);
            Rect rect = GUILayoutUtility.GetRect(0, 68, GUILayout.ExpandWidth(true));
            Texture texture = effective != null ? effective.mainTexture : null;
            if (texture != null)
                GUI.DrawTexture(new Rect(rect.x, rect.y, 64, 64), texture, ScaleMode.ScaleToFit);
            var label = new Rect(rect.x + 74, rect.y + 5, Mathf.Max(1, rect.width - 74), 55);
            GUI.Label(label, effective != null ? effective.name + "\nLeft / right follows spline direction." :
                "No material assigned.\nChoose a preset or your own material.", RoadToolsWindowStyles.Body);
        }
    }
}
