using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadPropPathKnotUtility
    {
        internal static bool InsertSelected(RoadPropPath path, bool after)
        {
            if (!TrySelected(path, out SplineContainer container, out int splineIndex, out int knotIndex)) return false;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            string label = after ? "Add Prop Point After" : "Add Prop Point Before";
            Undo.SetCurrentGroupName(label);
            Undo.RegisterCompleteObjectUndo(container, label);
            Spline spline = container.Splines[splineIndex];
            int inserted;
            if (!spline.Closed && ((after && knotIndex == spline.Count - 1) || (!after && knotIndex == 0)))
                inserted = Extend(container, spline, after);
            else
            {
                int next = after ? (knotIndex + 1) % spline.Count : knotIndex;
                int previous = (next + spline.Count - 1) % spline.Count;
                inserted = Split(spline, previous, next);
            }
            Finish(path, container);
            SplineSelection.Set(new SelectableKnot(new SplineInfo(container, splineIndex), inserted));
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            return true;
        }

        // Freeze only the split endpoints. De Casteljau retains the original curve and
        // their opposite handles, so moving the new point affects its two adjacent spans.
        private static int Split(Spline spline, int previous, int next)
        {
            BezierKnot a = spline[previous], b = spline[next];
            var curve = new BezierCurve(a, b);
            float3 up = spline.GetCurveUpVector(previous, 0.5f);
            CurveUtility.Split(curve, 0.5f, out BezierCurve left, out BezierCurve right);
            spline.SetTangentMode(previous, TangentMode.Broken);
            spline.SetTangentMode(next, TangentMode.Broken);
            a.TangentOut = math.mul(math.inverse(a.Rotation), left.Tangent0);
            b.TangentIn = math.mul(math.inverse(b.Rotation), right.Tangent1);
            spline[previous] = a;
            spline[next] = b;
            quaternion rotation = quaternion.LookRotationSafe(right.Tangent0, up);
            quaternion inverse = math.inverse(rotation);
            var knot = new BezierKnot(left.P3, math.mul(inverse, left.Tangent1),
                math.mul(inverse, right.Tangent0), rotation);
            spline.Insert(next, knot, TangentMode.Broken);
            return next;
        }

        private static int Extend(SplineContainer container, Spline spline, bool after)
        {
            int index = after ? spline.Count - 1 : 0;
            BezierKnot endpoint = spline[index];
            float3 direction = after ? endpoint.Position - spline[index - 1].Position :
                endpoint.Position - spline[1].Position;
            float3 tangent = after ? math.rotate(endpoint.Rotation, endpoint.TangentOut) :
                math.rotate(endpoint.Rotation, endpoint.TangentIn);
            direction = math.normalizesafe(math.lengthsq(tangent) > 0.000001f ? tangent : direction,
                after ? math.forward() : -math.forward());
            float worldScale = container.transform.TransformVector((Vector3)direction).magnitude;
            float3 offset = direction * (5f / Mathf.Max(0.0001f, worldScale));
            float3 forward = after ? direction : -direction;
            quaternion rotation = quaternion.LookRotationSafe(forward, math.rotate(endpoint.Rotation, math.up()));
            float3 handle = math.mul(math.inverse(rotation), forward * math.length(offset) / 3f);
            spline.SetTangentMode(index, TangentMode.Broken);
            if (after) endpoint.TangentOut = math.mul(math.inverse(endpoint.Rotation), offset / 3f);
            else endpoint.TangentIn = math.mul(math.inverse(endpoint.Rotation), offset / 3f);
            spline[index] = endpoint;
            int inserted = after ? spline.Count : 0;
            spline.Insert(inserted, new BezierKnot(endpoint.Position + offset, -handle, handle, rotation), TangentMode.Broken);
            return inserted;
        }

        internal static bool SetSelectedMode(RoadPropPath path, TangentMode mode)
        {
            if (!TrySelected(path, out SplineContainer container, out int splineIndex, out int knotIndex)) return false;
            Undo.RecordObject(container, "Change Prop Point Curve");
            Spline spline = container.Splines[splineIndex];
            if (mode == TangentMode.Broken) RoadKnotRotationTool.PrepareKnot(spline, knotIndex);
            spline.SetTangentMode(knotIndex, mode);
            Finish(path, container);
            return true;
        }

        internal static bool TrySelected(RoadPropPath path, out SplineContainer container, out int splineIndex, out int knotIndex)
        {
            container = path != null ? path.GetComponent<SplineContainer>() : null;
            splineIndex = knotIndex = -1;
            return !EditorApplication.isPlayingOrWillChangePlaymode && path != null && path.Owner != null && !path.Owner.IsBaked &&
                RoadPropSectionsUtility.TryGetSelectedKnot(container, out splineIndex, out knotIndex) &&
                container.Splines[splineIndex].Count >= 2;
        }

        private static void Finish(RoadPropPath path, SplineContainer container)
        {
            EditorUtility.SetDirty(container);
            PrefabUtility.RecordPrefabInstancePropertyModifications(container);
            EditorSceneManager.MarkSceneDirty(container.gameObject.scene);
            path.RequestRebuild();
            SceneView.RepaintAll();
        }
    }
}
