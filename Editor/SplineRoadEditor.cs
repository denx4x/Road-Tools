using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Splines;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    [CustomEditor(typeof(SplineRoad))]
    public sealed class SplineRoadEditor : UnityEditor.Editor
    {
        private static bool activatedSplineContext;
        private string actionStatus;
        private MessageType actionStatusType;

        private void OnEnable()
        {
            EditorApplication.delayCall += ActivateSplineEditing;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= ActivateSplineEditing;
            EditorApplication.delayCall += RevertSplineContextIfNoRoadSelected;
        }

        private static void RevertSplineContextIfNoRoadSelected()
        {
            if (!activatedSplineContext || ToolManager.activeContextType != typeof(SplineToolContext))
                return;

            foreach (GameObject selected in Selection.gameObjects)
            {
                if (selected != null && selected.GetComponent<SplineRoad>() != null)
                    return;
            }

            ToolManager.SetActiveContext<GameObjectToolContext>();
            activatedSplineContext = false;
        }

        private void ActivateSplineEditing()
        {
            if (Application.isBatchMode || this == null || target == null ||
                Selection.activeGameObject != ((SplineRoad)target).gameObject)
                return;

            if (ToolManager.activeContextType == typeof(SplineToolContext))
                return;

            ToolManager.SetActiveContext<SplineToolContext>();
            activatedSplineContext = true;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            var road = (SplineRoad)target;

            if (!road.IsBaked)
            {
                EditorGUILayout.HelpBox("Move spline points to update the road and registered terrain live. Select a point and press E to rotate its curve handles.", MessageType.Info);
                EditorGUILayout.LabelField("Live Status", RoadLiveUpdateCoordinator.GetStatus(road), EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUI.DisabledScope(!RoadDirectionPresetTool.TryGetSelectedKnot(road, out _, out _)))
                {
                    if (GUILayout.Button("Rotate Selected Knot (E)"))
                        RoadKnotRotationTool.Activate(road);
                }
            }

            if (road.IsBaked)
            {
                EditorGUILayout.HelpBox(
                    "This road is baked. The generated meshes are saved as assets. Resume editing to change the spline or rebuild the road.",
                    MessageType.Info);
                if (GUILayout.Button("Resume Editing", GUILayout.Height(28)))
                {
                    RoadBuildReport result = road.ResumeEditing();
                    if (result.Succeeded)
                        EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                    SetStatus(result.Message, result.Succeeded);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Auto Fix Clipping adjusts positions and tangents to smooth the road while preserving every knot. Changes support Undo.",
                    MessageType.Info);
                if (GUILayout.Button("Auto Fix Clipping", GUILayout.Height(28)))
                {
                    RoadClippingFixReport result = RoadClippingFixer.Fix(road);
                    SetStatus(
                        $"Adjusted {result.AdjustedKnots} knots; smoothed {result.SmoothedKnots} bends. All knots preserved. {result.Build.Message}" +
                        (result.RemainingTightCorner ? " Some overlap or tight curvature remains. Adjust the route or width." : ""),
                        result.Succeeded);
                }

                if (GUILayout.Button("Refresh Now (Recovery)", GUILayout.Height(24)))
                {
                    RoadBuildReport result = road.Rebuild();
                    if (result.Succeeded)
                        EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                    SetStatus(result.Message, result.Succeeded);
                }

                if (GUILayout.Button("Bake Generated Meshes", GUILayout.Height(28)))
                {
                    RoadBakeReport result = RoadBakeUtility.Bake(road);
                    SetStatus(result.Message, result.Succeeded);
                    if (result.Succeeded)
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<DefaultAsset>(result.Folder));
                }
            }

            if (!string.IsNullOrEmpty(actionStatus))
                EditorGUILayout.HelpBox(actionStatus, actionStatusType);
        }

        private void SetStatus(string message, bool succeeded)
        {
            actionStatus = message;
            actionStatusType = succeeded ? MessageType.Info : MessageType.Error;
            Repaint();
        }

        private void OnSceneGUI()
        {
            var road = (SplineRoad)target;
            if (!road.IsBaked)
                SplineRoadSceneGUI.Draw(road);
        }
    }
}
