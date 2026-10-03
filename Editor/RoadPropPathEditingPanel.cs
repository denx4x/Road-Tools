using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadPropPathEditingPanel
    {
        internal static void Draw(RoadPropPath path)
        {
            bool selected = RoadPropPathKnotUtility.TrySelected(path, out SplineContainer container, out int splineIndex, out int knotIndex);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(selected ? $"Point {knotIndex + 1} / {container.Splines[splineIndex].Count}" :
                "Select a path point in Scene View", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!selected))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Point Before", GUILayout.Height(26))) RoadPropPathKnotUtility.InsertSelected(path, false);
                if (GUILayout.Button("+ Point After", GUILayout.Height(26))) RoadPropPathKnotUtility.InsertSelected(path, true);
                EditorGUILayout.EndHorizontal();
                selected = RoadPropPathKnotUtility.TrySelected(path, out container, out splineIndex, out knotIndex);
                EditorGUILayout.Space(4);
                TangentMode mode = selected ? container.Splines[splineIndex].GetTangentMode(knotIndex) : TangentMode.Broken;
                EditorGUILayout.BeginHorizontal();
                DrawMode(path, "Linear", TangentMode.Linear, mode);
                DrawMode(path, "Auto Smooth", TangentMode.AutoSmooth, mode);
                DrawMode(path, "Bezier", TangentMode.Broken, mode);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField("Select a knot to edit locally. Moving the path GameObject moves all props. New points preserve the curve; Bezier gives local control.", RoadToolsWindowStyles.Body);
        }

        private static void DrawMode(RoadPropPath path, string label, TangentMode value, TangentMode selected)
        {
            bool active = value == TangentMode.Broken ? selected != TangentMode.Linear && selected != TangentMode.AutoSmooth : selected == value;
            if (GUILayout.Toggle(active, label, EditorStyles.miniButton, GUILayout.Height(24)) && !active)
                RoadPropPathKnotUtility.SetSelectedMode(path, value);
        }
    }
}
