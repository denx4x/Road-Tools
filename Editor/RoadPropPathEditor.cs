using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    [CustomEditor(typeof(RoadPropPath))]
    public sealed class RoadPropPathEditor : UnityEditor.Editor
    {
        private void OnEnable() => EditorApplication.delayCall += ActivateIfSelected;
        private void OnDisable() => EditorApplication.delayCall -= ActivateIfSelected;

        public override void OnInspectorGUI()
        {
            var path = (RoadPropPath)target;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Road", path.Owner, typeof(SplineRoad), true);
            EditorGUILayout.HelpBox("This spline controls one prop layer. Move its knots to bend the fence or other props. The road shape stays the same.", MessageType.Info);
            using (new EditorGUI.DisabledScope(path.Owner == null || path.Owner.IsBaked))
            {
                if (GUILayout.Button("Edit Path Knots", GUILayout.Height(26))) SelectPath(path);
                RoadPropPathEditingPanel.Draw(path);
                bool selectedKnot = RoadPropSectionsUtility.TryGetSelectedKnot(path.GetComponent<SplineContainer>(), out _, out _);
                using (new EditorGUI.DisabledScope(!selectedKnot))
                    if (GUILayout.Button("Rotate Selected Path Knot", GUILayout.Height(24))) RotateSelected(path);
            }
            if (path.Owner != null && GUILayout.Button("Select Road")) Selection.activeGameObject = path.Owner.gameObject;
        }

        internal static void SelectPath(RoadPropPath path)
        {
            if (path == null) return;
            Selection.activeGameObject = path.gameObject;
            EditorGUIUtility.PingObject(path.gameObject);
            EditorApplication.delayCall += () => Activate(path, false);
        }

        internal static bool RotateSelected(RoadPropPath path)
        {
            if (path == null || path.Owner == null || path.Owner.IsBaked) return false;
            SplineContainer container = path.GetComponent<SplineContainer>();
            if (!RoadPropSectionsUtility.TryGetSelectedKnot(container, out int splineIndex, out int knotIndex)) return false;
            Spline spline = container.Splines[splineIndex];
            Undo.RecordObject(container, "Rotate Prop Path Knot");
            RoadKnotRotationTool.PrepareKnot(spline, knotIndex);
            EditorUtility.SetDirty(container);
            EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
            Activate(path, true);
            return true;
        }

        private void ActivateIfSelected()
        {
            if (this != null && target != null && Selection.activeGameObject == ((RoadPropPath)target).gameObject)
                Activate((RoadPropPath)target, false);
        }

        private static void Activate(RoadPropPath path, bool rotate)
        {
            if (Application.isBatchMode || path == null || path.Owner == null || path.Owner.IsBaked ||
                Selection.activeGameObject != path.gameObject) return;
            if (ToolManager.activeContextType != typeof(SplineToolContext)) ToolManager.SetActiveContext<SplineToolContext>();
            if (rotate) ToolManager.SetActiveTool<SplineRotateTool>();
            else ToolManager.SetActiveTool<SplineMoveTool>();
            SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            var path = (RoadPropPath)target;
            if (path == null || path.Owner == null || path.Owner.IsBaked) return;
            SplineContainer container = path.GetComponent<SplineContainer>();
            if (container == null) return;
            Handles.Label(path.transform.position, path.name + " (Prop Path)");
            Event current = Event.current;
            if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.E &&
                !current.alt && !current.control && !current.command && !current.shift && !EditorGUIUtility.editingTextField &&
                RotateSelected(path)) current.Use();
        }
    }
}
