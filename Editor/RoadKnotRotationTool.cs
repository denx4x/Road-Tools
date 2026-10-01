using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    // Keep Unity's spline handles and shortcuts. Only unlock automatic tangents when
    // a rotation gesture starts, so drawing and moving points retain AutoSmooth.
    [InitializeOnLoad]
    internal static class RoadKnotRotationTool
    {
        static RoadKnotRotationTool()
        {
            SceneView.beforeSceneGui += BeforeSceneGUI;
        }

        internal static bool Activate(SplineRoad road)
        {
            if (road == null || road.IsBaked ||
                !RoadDirectionPresetTool.TryGetSelectedKnot(road, out _, out _))
                return false;

            if (ToolManager.activeContextType != typeof(SplineToolContext))
                ToolManager.SetActiveContext<SplineToolContext>();
            ToolManager.SetActiveTool<SplineRotateTool>();
            SceneView.RepaintAll();
            return true;
        }

        private static void BeforeSceneGUI(SceneView view)
        {
            Event current = Event.current;
            if (current == null || Application.isPlaying || EditorApplication.isCompiling)
                return;

            // E also exits Unity's Draw Spline tool. Consume only when a Road Tools
            // knot is selected; normal GameObject and other spline tools keep E.
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.E &&
                !current.alt && !current.control && !current.command && !current.shift &&
                !EditorGUIUtility.editingTextField)
            {
                foreach (GameObject selected in Selection.gameObjects)
                {
                    SplineRoad road = selected != null ? selected.GetComponent<SplineRoad>() : null;
                    if (!Activate(road))
                        continue;
                    current.Use();
                    return;
                }
            }

            if (ToolManager.activeToolType != typeof(SplineRotateTool) ||
                current.type != EventType.MouseDrag || GUIUtility.hotControl == 0)
                return;

            // Run before the native rotation handle updates its quaternion. Recording
            // in the gesture's current Undo group includes tangent conversion in Undo.
            foreach (GameObject selected in Selection.gameObjects)
            {
                SplineRoad road = selected != null ? selected.GetComponent<SplineRoad>() : null;
                if (road != null && !road.IsBaked)
                    PrepareSelectedKnots(road);
            }
        }

        internal static int PrepareSelectedKnots(SplineRoad road)
        {
            if (road == null || road.IsBaked)
                return 0;
            SplineContainer container = road.GetComponent<SplineContainer>();
            if (container == null || SplineSelection.Count == 0)
                return 0;

            var infos = new List<SplineInfo>(container.Splines.Count);
            for (int i = 0; i < container.Splines.Count; i++)
                infos.Add(new SplineInfo(container, i));
            var selected = new List<SelectableKnot>();
            SplineSelection.GetElements(infos, selected);
            int converted = 0;
            foreach (SelectableKnot selection in selected)
            {
                if ((Object)selection.SplineInfo.Container != container || !selection.IsValid())
                    continue;
                Spline spline = selection.SplineInfo.Spline;
                TangentMode mode = spline.GetTangentMode(selection.KnotIndex);
                if (mode != TangentMode.AutoSmooth && mode != TangentMode.Linear)
                    continue;
                if (converted == 0)
                    Undo.RecordObject(container, "Rotate Road Knot");
                PrepareKnot(spline, selection.KnotIndex);
                converted++;
            }
            if (converted > 0)
            {
                EditorUtility.SetDirty(container);
                EditorSceneManager.MarkSceneDirty(container.gameObject.scene);
                road.RequestRebuild();
            }
            return converted;
        }

        internal static bool PrepareKnot(Spline spline, int knotIndex)
        {
            if (spline == null || knotIndex < 0 || knotIndex >= spline.Count)
                return false;
            TangentMode mode = spline.GetTangentMode(knotIndex);
            if (mode != TangentMode.AutoSmooth && mode != TangentMode.Linear)
                return false;

            BezierKnot knot = spline[knotIndex];
            if (mode == TangentMode.Linear)
            {
                // Linear tangents are zero. Explicit handles along each adjacent
                // chord keep the straight path and allow rotating both with the knot.
                quaternion inverse = math.inverse(knot.Rotation);
                int previous = knotIndex > 0 ? knotIndex - 1 : spline.Closed ? spline.Count - 1 : knotIndex;
                int next = knotIndex + 1 < spline.Count ? knotIndex + 1 : spline.Closed ? 0 : knotIndex;
                knot.TangentIn = math.mul(inverse, (spline[previous].Position - knot.Position) / 3f);
                knot.TangentOut = math.mul(inverse, (spline[next].Position - knot.Position) / 3f);
            }
            // Broken does not rewrite either handle. AutoSmooth's evaluated shape is
            // preserved, and native knot rotation rotates both handles together.
            spline.SetTangentMode(knotIndex, TangentMode.Broken);
            spline[knotIndex] = knot;
            return true;
        }
    }
}
