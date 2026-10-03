using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadToolsPropSectionsPanel
    {
        private static int roadId;
        private static int selectedLayer;
        private static float gapLength = 5f;
        private static PropSide gapSide = PropSide.Both;
        private static PropSide pathSide = PropSide.Left;
        private static int pathSplineIndex;
        private static string actionStatus;

        internal static void DrawGeneration(SplineRoad road, PropLayerManager manager)
        {
            bool enabled = EditorGUILayout.ToggleLeft("Generate Props on This Road", manager.PropsEnabled, EditorStyles.boldLabel);
            if (enabled != manager.PropsEnabled) RoadPropSectionsUtility.SetPropsEnabled(road, enabled);
            if (!manager.PropsEnabled)
                EditorGUILayout.HelpBox("Props are disabled on this road. Its layers and editable paths are retained.", MessageType.Info);
        }

        internal static void Draw(SplineRoad road, PropLayerManager manager)
        {
            EditorGUILayout.LabelField("PLACEMENT & GAPS", RoadToolsWindowStyles.SectionTitle);
            if (manager.Layers.Count == 0)
            {
                EditorGUILayout.LabelField("Add a preset or a prop layer to choose its sides and gaps.", RoadToolsWindowStyles.Body);
                return;
            }
            if (roadId != road.GetInstanceID())
            {
                roadId = road.GetInstanceID();
                selectedLayer = 0;
                actionStatus = null;
            }
            selectedLayer = Mathf.Clamp(selectedLayer, 0, manager.Layers.Count - 1);
            var labels = new string[manager.Layers.Count];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = manager.Layers[i].Name + (manager.Layers[i].Enabled ? "" : " (disabled)");
            selectedLayer = EditorGUILayout.Popup("Prop Layer", selectedLayer, labels);

            var serialized = new SerializedObject(manager);
            SerializedProperty layer = serialized.FindProperty("layers").GetArrayElementAtIndex(selectedLayer);
            EditorGUILayout.PropertyField(layer.FindPropertyRelative("enabled"), new GUIContent("Enable This Layer"));
            if (manager.Layers[selectedLayer].UsesCustomPath && manager.Layers[selectedLayer].CustomPath == null)
            {
                if (serialized.ApplyModifiedProperties()) RoadPropPresetUtility.Refresh(road, manager);
                EditorGUILayout.HelpBox("This layer's editable path is missing. Restore the deleted path with Undo, assign a path in Advanced Prop Layers, or remove this layer.", MessageType.Warning);
                return;
            }
            EditorGUILayout.PropertyField(layer.FindPropertyRelative("side"), new GUIContent("Side"));
            SplineContainer customPath = layer.FindPropertyRelative("customPath").objectReferenceValue as SplineContainer;
            SplineContainer container = customPath != null ? customPath : road.GetComponent<SplineContainer>();
            DrawSplineSelector(layer.FindPropertyRelative(customPath != null ? "customSplineIndex" : "splineIndex"),
                container, customPath == null, customPath != null ? "Path Spline" : "Road Spline");
            if (serialized.ApplyModifiedProperties()) RoadPropPresetUtility.Refresh(road, manager);

            using (new EditorGUI.DisabledScope(manager.Layers[selectedLayer].Side != PropSide.Both))
                if (GUILayout.Button("Split into Left / Right Layers", GUILayout.Height(24)))
                {
                    RoadPropSectionsUtility.SplitSides(road, selectedLayer);
                    GUIUtility.ExitGUI();
                }
            EditorGUILayout.LabelField("Split sides to give each side its own prefab, spacing, gaps, and path.", RoadToolsWindowStyles.Body);
            EditorGUILayout.Space(8);
            DrawGaps(road, manager, container, customPath != null);
            EditorGUILayout.Space(8);
            DrawPath(road, manager);
            if (!string.IsNullOrEmpty(actionStatus)) EditorGUILayout.HelpBox(actionStatus, MessageType.Info);
        }

        private static void DrawGaps(SplineRoad road, PropLayerManager manager, SplineContainer container, bool customPath)
        {
            EditorGUILayout.LabelField("Gaps / Openings", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Leave an entrance clear on one side or both. Distances are measured along the selected spline.", RoadToolsWindowStyles.Body);
            SplinePropLayer selected = manager.Layers[selectedLayer];
            if (selected.Side != PropSide.Both) gapSide = selected.Side;
            using (new EditorGUI.DisabledScope(selected.Side != PropSide.Both))
                gapSide = (PropSide)EditorGUILayout.EnumPopup("Gap Side", gapSide);
            if (selected.Side == PropSide.Both && gapSide == PropSide.Center) gapSide = PropSide.Both;
            gapLength = Mathf.Max(0.1f, EditorGUILayout.FloatField("Gap Length (m)", gapLength));
            bool hasSelectedKnot = RoadPropSectionsUtility.TryGetSelectedKnot(container, out _, out _);
            using (new EditorGUI.DisabledScope(!hasSelectedKnot))
                if (GUILayout.Button("Add Gap at Selected Knot", GUILayout.Height(26)))
                {
                    RoadPropSectionsUtility.AddGapAtSelectedKnot(road, selectedLayer, gapLength, gapSide, out actionStatus);
                    GUIUtility.ExitGUI();
                }

            var serialized = new SerializedObject(manager);
            SerializedProperty layer = serialized.FindProperty("layers").GetArrayElementAtIndex(selectedLayer);
            SerializedProperty gaps = layer.FindPropertyRelative("gaps");
            for (int i = 0; i < gaps.arraySize; i++)
            {
                SerializedProperty gap = gaps.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Gap {i + 1}", EditorStyles.boldLabel);
                bool remove = GUILayout.Button("Remove", GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();
                DrawSplineSelector(gap.FindPropertyRelative("splineIndex"), container, true,
                    customPath ? "Path Spline" : "Road Spline");
                EditorGUILayout.PropertyField(gap.FindPropertyRelative("side"), new GUIContent("Side"));
                SerializedProperty entireSpline = gap.FindPropertyRelative("entireSpline");
                EditorGUILayout.PropertyField(entireSpline, new GUIContent("Disable Entire Spline"));
                if (!entireSpline.boolValue)
                {
                    SerializedProperty start = gap.FindPropertyRelative("startDistance");
                    SerializedProperty end = gap.FindPropertyRelative("endDistance");
                    start.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField("From (m)", start.floatValue));
                    end.floatValue = Mathf.Max(start.floatValue, EditorGUILayout.FloatField("To (m)", end.floatValue));
                }
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    gaps.DeleteArrayElementAtIndex(i);
                    serialized.ApplyModifiedProperties();
                    RoadPropPresetUtility.Refresh(road, manager);
                    GUIUtility.ExitGUI();
                }
            }
            if (serialized.ApplyModifiedProperties()) RoadPropPresetUtility.Refresh(road, manager);
            if (GUILayout.Button("+ Add Gap by Distance", GUILayout.Height(24)))
            {
                int sourceIndex = customPath ? selected.CustomSplineIndex : selected.SplineIndex;
                RoadPropSectionsUtility.AddGap(road, selectedLayer, sourceIndex, gapSide, 0f, gapLength);
                GUIUtility.ExitGUI();
            }
        }

        private static void DrawPath(SplineRoad road, PropLayerManager manager)
        {
            SplinePropLayer layer = manager.Layers[selectedLayer];
            EditorGUILayout.LabelField("Editable Prop Path", EditorStyles.boldLabel);
            if (layer.UsesCustomPath && layer.CustomPath == null)
            {
                EditorGUILayout.HelpBox("The assigned editable path is missing. Restore it with Undo or assign a path in Advanced Prop Layers.", MessageType.Warning);
                return;
            }
            if (layer.CustomPath != null)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("Path", layer.CustomPath, typeof(SplineContainer), true);
                RoadPropPath path = layer.CustomPath.GetComponent<RoadPropPath>();
                if (path != null && GUILayout.Button("Select / Edit Path", GUILayout.Height(26)))
                    RoadPropPathEditor.SelectPath(path);
                else if (path == null)
                    EditorGUILayout.HelpBox("This layer uses an external spline. Select that object to edit its knots.", MessageType.Info);
                EditorGUILayout.LabelField("Move path knots to bend these props independently from the road.", RoadToolsWindowStyles.Body);
                if (path != null) RoadPropPathEditingPanel.Draw(path);
                return;
            }

            EditorGUILayout.LabelField("Create an independent spline for a fence to turn into an entrance or follow a custom route.", RoadToolsWindowStyles.Body);
            if (layer.Side != PropSide.Both) pathSide = layer.Side;
            else if (pathSide != PropSide.Left && pathSide != PropSide.Right) pathSide = PropSide.Left;
            using (new EditorGUI.DisabledScope(layer.Side != PropSide.Both))
                pathSide = (PropSide)EditorGUILayout.EnumPopup("Path Side", pathSide);
            if (layer.Side == PropSide.Both && pathSide != PropSide.Left && pathSide != PropSide.Right) pathSide = PropSide.Left;
            SplineContainer source = road.GetComponent<SplineContainer>();
            pathSplineIndex = layer.SplineIndex >= 0 ? layer.SplineIndex : pathSplineIndex;
            using (new EditorGUI.DisabledScope(layer.SplineIndex >= 0))
                pathSplineIndex = DrawSplineIndex("Road Spline", pathSplineIndex, source, false);
            using (new EditorGUI.DisabledScope(source == null || source.Splines.Count == 0))
                if (GUILayout.Button("Create Editable Path for This Side", GUILayout.Height(26)))
                {
                    RoadPropPath path = RoadPropSectionsUtility.CreateEditablePath(road, selectedLayer, pathSide, pathSplineIndex, out actionStatus);
                    if (path != null)
                    {
                        selectedLayer = manager.Layers.Count - 1;
                        RoadPropPathEditor.SelectPath(path);
                    }
                    GUIUtility.ExitGUI();
                }
        }

        private static void DrawSplineSelector(SerializedProperty property, SplineContainer container, bool allowAll, string label)
            => property.intValue = DrawSplineIndex(label, property.intValue, container, allowAll);

        private static int DrawSplineIndex(string label, int value, SplineContainer container, bool allowAll)
        {
            int count = container != null ? container.Splines.Count : 0;
            if (count == 0)
            {
                EditorGUILayout.LabelField(label, "No spline");
                return value;
            }
            int offset = allowAll ? 1 : 0;
            var labels = new string[count + offset];
            if (allowAll) labels[0] = "All Road Splines";
            for (int i = 0; i < count; i++) labels[i + offset] = "Spline " + (i + 1);
            int selected = Mathf.Clamp(value + offset, 0, labels.Length - 1);
            return EditorGUILayout.Popup(label, selected, labels) - offset;
        }
    }
}
