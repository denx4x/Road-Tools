using System;
using UnityEditor;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadToolsDirectionPanel
    {
        private static readonly string[] Sides = { "Left", "Right" };
        private static readonly float[] Angles = { 0f, 30f, 45f, 60f, 90f };

        internal static void Draw(SplineRoad road, RoadTurnPreview preview, ref float length,
            ref float radius, ref float angle, ref bool show, ref bool left, Action<string, bool> status)
        {
            if (road.Profile == null || !RoadDirectionPresetTool.TryGetSelectedKnot(road, out _, out int knot))
            {
                EditorGUILayout.LabelField(road.Profile == null ? "Assign a Road Profile first." :
                    "Select one road knot in Scene View to preview its next segment.", RoadToolsWindowStyles.Body);
                return;
            }

            EditorGUILayout.LabelField($"Point {knot + 1} · Next segment", EditorStyles.boldLabel);
            length = Mathf.Max(0.5f, EditorGUILayout.FloatField("Length (m)", length));
            radius = EditorGUILayout.FloatField("Bend Radius (m)", radius);
            EditorGUILayout.LabelField($"Minimum radius: {road.Profile.Width * 0.5f + 0.5f:0.##} m",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6);
            int side = angle != 0f ? (angle < 0f ? 0 : 1) : (left ? 0 : 1);
            left = side == 0;
            int chosenSide = GUILayout.Toolbar(side, Sides, GUILayout.Height(24));
            if (chosenSide != side) { left = chosenSide == 0; angle = Mathf.Abs(angle) * (left ? -1f : 1f); show = true; }
            EditorGUILayout.BeginHorizontal();
            foreach (float preset in Angles)
                if (GUILayout.Button(preset == 0 ? "Straight" : preset.ToString("0") + "°", GUILayout.Height(26)))
                { angle = preset * (chosenSide == 0 ? -1f : 1f); show = true; }
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            angle = EditorGUILayout.FloatField("Angle (°)", angle);
            if (EditorGUI.EndChangeCheck() && angle != 0f) left = angle < 0f;
            show = EditorGUILayout.Toggle("Scene Preview", show);
            if (show)
            {
                preview.Update(road, length, angle, radius);
                EditorGUILayout.HelpBox(preview.Plan?.Message ?? "Select a point.",
                    preview.Plan != null && preview.Plan.Safe ? MessageType.Info : MessageType.Warning);
            }
            using (new EditorGUI.DisabledScope(!show || preview.Plan == null || !preview.Plan.Safe))
                if (GUILayout.Button("Apply Direction", GUILayout.Height(28)))
                {
                    bool success = RoadDirectionPresetTool.Apply(road, length, angle, radius, out string result);
                    status(result, success);
                    if (success) { show = false; preview.Clear(); }
                }
            if (GUI.changed) SceneView.RepaintAll();
        }
    }
}
