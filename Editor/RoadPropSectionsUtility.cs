using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadPropSectionsUtility
    {
        internal static bool SetPropsEnabled(SplineRoad road, bool enabled)
        {
            if (!CanEdit(road, out PropLayerManager manager)) return false;
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("propsEnabled").boolValue = enabled;
            return Apply(road, manager, serialized);
        }

        internal static bool SetLayerEnabled(SplineRoad road, int index, bool enabled)
        {
            if (!TryLayer(road, index, out PropLayerManager manager, out SerializedObject serialized, out SerializedProperty layer))
                return false;
            layer.FindPropertyRelative("enabled").boolValue = enabled;
            return Apply(road, manager, serialized);
        }

        internal static bool SplitSides(SplineRoad road, int index)
        {
            if (!TryLayer(road, index, out PropLayerManager manager, out _, out _) || manager.Layers[index].Side != PropSide.Both)
                return false;
            Undo.RecordObject(manager, "Split Road Prop Sides");
            string name = manager.Layers[index].Name;
            manager.DuplicateLayer(index);
            var serialized = new SerializedObject(manager);
            SerializedProperty layers = serialized.FindProperty("layers");
            SerializedProperty left = layers.GetArrayElementAtIndex(index);
            SerializedProperty right = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            left.FindPropertyRelative("name").stringValue = name + " Left";
            left.FindPropertyRelative("side").enumValueIndex = (int)PropSide.Left;
            right.FindPropertyRelative("name").stringValue = name + " Right";
            right.FindPropertyRelative("side").enumValueIndex = (int)PropSide.Right;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RoadPropPresetUtility.Refresh(road, manager);
            return true;
        }

        internal static bool AddGap(SplineRoad road, int index, int splineIndex, PropSide side, float startDistance, float endDistance)
        {
            if (!TryLayer(road, index, out PropLayerManager manager, out SerializedObject serialized, out SerializedProperty layer) ||
                !Finite(startDistance) || !Finite(endDistance)) return false;
            AppendGap(layer.FindPropertyRelative("gaps"), splineIndex, side, startDistance, endDistance);
            return Apply(road, manager, serialized);
        }

        internal static bool AddGapAtSelectedKnot(SplineRoad road, int index, float length, PropSide side, out string message)
        {
            if (!CanEdit(road, out PropLayerManager manager) || index < 0 || index >= manager.Layers.Count ||
                !Finite(length) || length <= 0f)
            {
                message = "Select an editable prop layer and use a positive gap length.";
                return false;
            }
            SplinePropLayer layer = manager.Layers[index];
            if (layer.UsesCustomPath && layer.CustomPath == null)
            {
                message = "This layer's editable path is missing. Restore the deleted path with Undo or remove this layer.";
                return false;
            }
            SplineContainer container = layer.CustomPath != null ? layer.CustomPath : road.GetComponent<SplineContainer>();
            if (!TryGetSelectedKnot(container, out int splineIndex, out int knotIndex))
            {
                message = layer.CustomPath != null ? "Select one knot on this layer's editable path." : "Select one road knot in the Scene view.";
                return false;
            }
            int requiredSpline = layer.CustomPath != null ? layer.CustomSplineIndex : layer.SplineIndex;
            if (requiredSpline >= 0 && requiredSpline != splineIndex)
            {
                message = "The selected knot is on a different spline from this prop layer.";
                return false;
            }
            List<SplineSamplingUtility.Sample> samples = SplineSamplingUtility.BuildArcLengthSamples(container, splineIndex, 0.25f);
            if (samples.Count < 2)
            {
                message = "The selected spline needs at least two knots.";
                return false;
            }
            float t = container.Splines[splineIndex].ConvertIndexUnit(knotIndex, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            float distance = DistanceAtT(samples, t);
            float pathLength = samples[samples.Count - 1].Distance;
            float start = Mathf.Max(0f, distance - length * 0.5f);
            float end = Mathf.Min(pathLength, distance + length * 0.5f);
            bool changed = AddGap(road, index, splineIndex, side, start, end);
            message = changed ? $"Added {side} gap from {start:0.##} to {end:0.##} m." : "The gap could not be added.";
            return changed;
        }

        internal static RoadPropPath CreateEditablePath(SplineRoad road, int index, PropSide side, out string message)
        {
            int sourceSpline = 0;
            if (road != null && road.TryGetComponent(out PropLayerManager manager) && index >= 0 && index < manager.Layers.Count)
            {
                sourceSpline = Mathf.Max(0, manager.Layers[index].SplineIndex);
                if (TryGetSelectedKnot(road.GetComponent<SplineContainer>(), out int selectedSpline, out _) &&
                    manager.Layers[index].SplineIndex < 0) sourceSpline = selectedSpline;
            }
            return CreateEditablePath(road, index, side, sourceSpline, out message);
        }

        internal static RoadPropPath CreateEditablePath(SplineRoad road, int index, PropSide side, int sourceSplineIndex, out string message)
        {
            message = "Select an editable prop layer with a valid road spline.";
            if (!CanEdit(road, out PropLayerManager manager) || index < 0 || index >= manager.Layers.Count) return null;
            SplinePropLayer original = manager.Layers[index];
            if (original.UsesCustomPath)
            {
                message = original.CustomPath != null ? "This layer already uses an editable path." :
                    "This layer's editable path is missing. Restore the deleted path with Undo or remove this layer.";
                return original.CustomPath != null ? original.CustomPath.GetComponent<RoadPropPath>() : null;
            }
            if (side == PropSide.Both || (original.Side != PropSide.Both && original.Side != side))
            {
                message = "Choose one side used by this layer.";
                return null;
            }
            SplineContainer source = road.GetComponent<SplineContainer>();
            if (source == null || sourceSplineIndex < 0 || sourceSplineIndex >= source.Splines.Count ||
                source.Splines[sourceSplineIndex].Count < 2 ||
                (original.SplineIndex >= 0 && original.SplineIndex != sourceSplineIndex)) return null;

            List<SplineSamplingUtility.Sample> samples = SplineSamplingUtility.BuildArcLengthSamples(source, sourceSplineIndex, 0.25f);
            if (samples.Count < 2) return null;
            Spline sourceSpline = source.Splines[sourceSplineIndex];
            var points = new List<Vector3>();
            // Include a midpoint per curve to retain the road's initial bend while keeping the path editable.
            int curves = sourceSpline.Closed ? sourceSpline.Count : sourceSpline.Count - 1;
            for (int i = 0; i < curves; i++)
            {
                AddOffsetPoint(points, source, sourceSplineIndex, sourceSpline, i, side, road.Profile, original);
                AddOffsetPoint(points, source, sourceSplineIndex, sourceSpline, i + 0.5f, side, road.Profile, original);
            }
            if (!sourceSpline.Closed)
                AddOffsetPoint(points, source, sourceSplineIndex, sourceSpline, sourceSpline.Count - 1, side, road.Profile, original);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Editable Prop Path");
            string label = original.Name + " " + side + " Path";
            var pathObject = new GameObject(label);
            Undo.RegisterCreatedObjectUndo(pathObject, "Create Editable Prop Path");
            Undo.SetTransformParent(pathObject.transform, road.transform, "Create Editable Prop Path");
            Undo.RecordObject(pathObject.transform, "Create Editable Prop Path");
            pathObject.transform.localPosition = Vector3.zero;
            pathObject.transform.localRotation = Quaternion.identity;
            pathObject.transform.localScale = Vector3.one;
            SplineContainer pathContainer = Undo.AddComponent<SplineContainer>(pathObject);
            RoadPropPath path = Undo.AddComponent<RoadPropPath>(pathObject);
            Undo.RecordObject(pathContainer, "Create Editable Prop Path");
            Undo.RecordObject(path, "Create Editable Prop Path");
            var spline = new Spline { Closed = sourceSpline.Closed };
            foreach (Vector3 point in points)
                spline.Add(new BezierKnot((float3)pathObject.transform.InverseTransformPoint(point)), TangentMode.AutoSmooth);
            pathContainer.Spline = spline;
            path.Configure(road);

            // Creating objects/components flushes RecordObject scopes. Capture the manager
            // after those operations so Undo also restores its layer list and source-side gap.
            Undo.RegisterCompleteObjectUndo(manager, "Create Editable Prop Path");
            manager.DuplicateLayer(index);
            var serialized = new SerializedObject(manager);
            SerializedProperty layers = serialized.FindProperty("layers");
            SerializedProperty pathLayer = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            pathLayer.FindPropertyRelative("name").stringValue = label;
            pathLayer.FindPropertyRelative("side").enumValueIndex = (int)PropSide.Center;
            pathLayer.FindPropertyRelative("lateralOffset").floatValue = 0f;
            pathLayer.FindPropertyRelative("splineIndex").intValue = -1;
            pathLayer.FindPropertyRelative("customPath").objectReferenceValue = pathContainer;
            pathLayer.FindPropertyRelative("customSplineIndex").intValue = 0;
            pathLayer.FindPropertyRelative("useCustomPath").boolValue = true;
            pathLayer.FindPropertyRelative("mirrorCustomPath").boolValue = side == PropSide.Right ||
                (side == PropSide.Center && original.MirrorCustomPath);
            SerializedProperty pathGaps = pathLayer.FindPropertyRelative("gaps");
            for (int i = pathGaps.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty gap = pathGaps.GetArrayElementAtIndex(i);
                int gapSpline = gap.FindPropertyRelative("splineIndex").intValue;
                PropSide gapSide = (PropSide)gap.FindPropertyRelative("side").enumValueIndex;
                if ((gapSpline >= 0 && gapSpline != sourceSplineIndex) || (gapSide != PropSide.Both && gapSide != side))
                    pathGaps.DeleteArrayElementAtIndex(i);
                else
                {
                    gap.FindPropertyRelative("side").enumValueIndex = (int)PropSide.Center;
                    gap.FindPropertyRelative("splineIndex").intValue = 0;
                }
            }
            // Suppress only this side of this source spline. Other splines and the opposite side stay intact.
            AppendGap(layers.GetArrayElementAtIndex(index).FindPropertyRelative("gaps"), sourceSplineIndex,
                side, 0f, samples[samples.Count - 1].Distance, true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pathContainer);
            EditorUtility.SetDirty(path);
            RoadPropPresetUtility.Refresh(road, manager);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(undoGroup);
            message = "Editable path created. Move its spline knots to bend these props independently.";
            return path;
        }

        internal static bool TryGetSelectedKnot(SplineContainer container, out int splineIndex, out int knotIndex)
        {
            splineIndex = knotIndex = -1;
            if (container == null || SplineSelection.Count == 0) return false;
            var infos = new List<SplineInfo>(container.Splines.Count);
            for (int i = 0; i < container.Splines.Count; i++) infos.Add(new SplineInfo(container, i));
            var knots = new List<SelectableKnot>();
            SplineSelection.GetElements(infos, knots);
            if (knots.Count != 1 || (Object)knots[0].SplineInfo.Container != container || !knots[0].IsValid()) return false;
            for (int i = 0; i < container.Splines.Count; i++)
                if (container.Splines[i] == knots[0].SplineInfo.Spline) { splineIndex = i; break; }
            knotIndex = knots[0].KnotIndex;
            return splineIndex >= 0;
        }

        private static void AddOffsetPoint(List<Vector3> points, SplineContainer container, int splineIndex,
            Spline spline, float knotUnit, PropSide side, RoadProfile profile, SplinePropLayer layer)
        {
            float t = spline.ConvertIndexUnit(knotUnit, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            container.Evaluate(splineIndex, t, out float3 position, out float3 tangent, out _);
            Vector3 right = Vector3.Cross(Vector3.up, ((Vector3)tangent).normalized).normalized;
            if (right.sqrMagnitude < 0.5f) right = Vector3.right;
            float sign = side == PropSide.Left ? -1f : side == PropSide.Right ? 1f : 0f;
            float offset = sign == 0f ? layer.LateralOffset : sign * (profile.Width * 0.5f + layer.LateralOffset);
            points.Add((Vector3)position + right * offset);
        }

        private static float DistanceAtT(IReadOnlyList<SplineSamplingUtility.Sample> samples, float t)
        {
            for (int i = 1; i < samples.Count; i++)
                if (samples[i].T >= t)
                    return Mathf.Lerp(samples[i - 1].Distance, samples[i].Distance,
                        Mathf.InverseLerp(samples[i - 1].T, samples[i].T, t));
            return samples[samples.Count - 1].Distance;
        }

        private static void AppendGap(SerializedProperty gaps, int splineIndex, PropSide side, float start, float end, bool entireSpline = false)
        {
            int index = gaps.arraySize;
            gaps.arraySize++;
            SerializedProperty gap = gaps.GetArrayElementAtIndex(index);
            gap.FindPropertyRelative("startDistance").floatValue = Mathf.Max(0f, Mathf.Min(start, end));
            gap.FindPropertyRelative("endDistance").floatValue = Mathf.Max(0f, Mathf.Max(start, end));
            gap.FindPropertyRelative("side").enumValueIndex = (int)side;
            gap.FindPropertyRelative("splineIndex").intValue = splineIndex;
            gap.FindPropertyRelative("entireSpline").boolValue = entireSpline;
        }

        private static bool TryLayer(SplineRoad road, int index, out PropLayerManager manager,
            out SerializedObject serialized, out SerializedProperty layer)
        {
            serialized = null;
            layer = null;
            if (!CanEdit(road, out manager) || index < 0 || index >= manager.Layers.Count) return false;
            serialized = new SerializedObject(manager);
            layer = serialized.FindProperty("layers").GetArrayElementAtIndex(index);
            return true;
        }

        private static bool CanEdit(SplineRoad road, out PropLayerManager manager)
        {
            manager = road != null ? road.GetComponent<PropLayerManager>() : null;
            return road != null && !road.IsBaked && road.Profile != null && manager != null;
        }

        private static bool Apply(SplineRoad road, PropLayerManager manager, SerializedObject serialized)
        {
            if (!serialized.ApplyModifiedProperties()) return false;
            RoadPropPresetUtility.Refresh(road, manager);
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
