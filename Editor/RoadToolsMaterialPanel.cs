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
            EditorGUILayout.LabelField(road.IsBaked
                ? "Material changes apply to this baked road immediately."
                : "Material changes apply to this road immediately.", RoadToolsWindowStyles.Body);
        }

        internal static void DrawTemplate(ref Material selected, RoadProfile profile)
        {
            DrawChoice(ref selected, profile);
            EditorGUILayout.LabelField("Used by the next road you create. None uses the Road Profile material.",
                RoadToolsWindowStyles.Body);
        }

        private static void DrawChoice(ref Material selected, RoadProfile profile)
        {
            Material profileMaterial = profile != null ? profile.Material : null;
            EditorGUI.BeginChangeCheck();
            Material chosen = (Material)EditorGUILayout.ObjectField("Road Material", selected,
                typeof(Material), false);
            if (EditorGUI.EndChangeCheck()) selected = chosen;

            EditorGUILayout.Space(5);
            Material marked = RoadToolsPackagePaths.LoadDefault<Material>("Materials/Road Asphalt Marked.mat");
            bool wide = EditorGUIUtility.currentViewWidth >= 400f;
            if (wide) EditorGUILayout.BeginHorizontal();
            if (DrawCard("Profile Default", profileMaterial, selected == null)) selected = null;
            if (marked != null && DrawCard("Marked Asphalt", marked, selected == marked)) selected = marked;
            if (wide) EditorGUILayout.EndHorizontal();
            if (selected != null && GUILayout.Button("Reset to Profile Material")) selected = null;
        }

        private static bool DrawCard(string label, Material material, bool selected)
        {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.miniButton,
                GUILayout.MinWidth(150f), GUILayout.ExpandWidth(true), GUILayout.Height(62f));
            Color previous = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.55f, 0.78f, 1f) : new Color(0.62f, 0.81f, 1f);
            bool clicked = GUI.Button(rect, GUIContent.none, EditorStyles.miniButton);
            GUI.backgroundColor = previous;
            Texture preview = material != null
                ? AssetPreview.GetAssetPreview(material) ?? AssetPreview.GetMiniThumbnail(material)
                : EditorGUIUtility.IconContent("Material Icon").image;
            if (preview != null)
                GUI.DrawTexture(new Rect(rect.x + 7f, rect.y + 7f, 48f, 48f), preview, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(rect.x + 62f, rect.y + 8f, Mathf.Max(10f, rect.width - 68f), 22f), label,
                EditorStyles.boldLabel);
            GUI.Label(new Rect(rect.x + 62f, rect.y + 30f, Mathf.Max(10f, rect.width - 68f), 24f),
                material != null ? material.name : "No profile material", RoadToolsWindowStyles.Body);
            return clicked;
        }
    }
}
