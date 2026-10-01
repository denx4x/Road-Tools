using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    // A detached spline is shared by preview and Apply, so both use identical geometry.
    internal sealed class RoadTurnPlan
    {
        internal Spline Spline;
        internal int SplineIndex, EndIndex;
        internal Vector3[] Center, Left, Right;
        internal bool Safe;
        internal string Message;

        internal static RoadTurnPlan Create(SplineRoad road, float length, float angle, float radius)
        {
            var plan = new RoadTurnPlan();
            if (road == null || road.Profile == null || road.IsBaked ||
                !RoadDirectionPresetTool.TryGetSelectedKnot(road, out int si, out int ki))
                return Fail(plan, "Select exactly one point on an editable road.");
            var container = road.GetComponent<SplineContainer>();
            var original = container.Splines[si];
            if (original.Closed || original.Count < 2)
                return Fail(plan, "Use an open spline with at least two points.");
            if (!Finite(length) || !Finite(angle) || !Finite(radius) || length < 0.5f || Mathf.Abs(angle) > 120f)
                return Fail(plan, "Use a finite length >= 0.5 m and an angle between -120 and 120 degrees.");
            bool turn = Mathf.Abs(angle) > 0.01f;
            float minimum = road.Profile.Width * 0.5f + 0.5f;
            if (turn && radius < minimum)
                return Fail(plan, $"Radius must be at least {minimum:0.##} m for this road width.");
            Transform tr = container.transform;
            Vector3 start = tr.TransformPoint((Vector3)original[ki].Position);
            Vector3 forward = ki > 0 ? start - tr.TransformPoint((Vector3)original[ki - 1].Position)
                : tr.TransformPoint((Vector3)original[1].Position) - start;
            forward.y = 0;
            if (forward.sqrMagnitude < 0.001f) return Fail(plan, "Adjacent points are too close.");
            // Prefer the authored incoming tangent so successive arcs join smoothly.
            if (ki > 0)
            {
                BezierKnot k = original[ki];
                Vector3 incoming = tr.TransformVector((Vector3)math.rotate(k.Rotation, -k.TangentIn));
                incoming.y = 0;
                if (incoming.sqrMagnitude > 0.001f) forward = incoming;
            }
            forward.Normalize();
            var spline = new Spline(original);
            for (int i = 0; i < original.Count; i++)
            {
                spline.SetTangentModeNoNotify(i, original.GetTangentMode(i));
                spline.SetAutoSmoothTensionNoNotify(i, original.GetAutoSmoothTension(i));
                spline.SetKnotNoNotify(i, original[i]);
            }
            plan.Spline = spline; plan.SplineIndex = si;
            int index = ki;
            Vector3 position = start;
            int arcs = turn ? Mathf.CeilToInt(Mathf.Abs(angle) / 45f) : 0;
            float step = arcs > 0 ? angle / arcs : 0;
            for (int i = 0; i < arcs; i++)
            {
                Vector3 nextForward = Quaternion.AngleAxis(step, Vector3.up) * forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                Vector3 nextRight = Vector3.Cross(Vector3.up, nextForward);
                Vector3 nextPosition = position + Mathf.Sign(step) * radius * (right - nextRight);
                float handle = 4f / 3f * radius * Mathf.Tan(Mathf.Abs(step) * Mathf.Deg2Rad / 4f);
                SetOutgoing(spline, index, tr, forward * handle);
                spline.Insert(++index, Knot(tr, Surface(road, nextPosition), -nextForward * handle, Vector3.zero), TangentMode.Broken);
                position = nextPosition; forward = nextForward;
            }
            Vector3 target = Surface(road, position + forward * length);
            SetOutgoing(spline, index, tr, forward * length / 3f);
            int end = index + 1;
            if (end == spline.Count)
                spline.Add(Knot(tr, target, -forward * length / 3f, Vector3.zero), TangentMode.Broken);
            else
            {
                BezierKnot next = spline[end];
                Vector3 outgoing = tr.TransformVector((Vector3)math.rotate(next.Rotation, next.TangentOut));
                spline.SetTangentMode(end, TangentMode.Broken);
                spline.SetKnot(end, Knot(tr, target, -forward * length / 3f, outgoing));
            }
            plan.EndIndex = end;
            RoadTurnValidation.SampleAndValidate(plan, container, road.Profile.Width);
            return plan;
        }

        private static void SetOutgoing(Spline spline, int index, Transform tr, Vector3 outgoing)
        {
            BezierKnot old = spline[index];
            Vector3 incoming = tr.TransformVector((Vector3)math.rotate(old.Rotation, old.TangentIn));
            spline.SetTangentMode(index, TangentMode.Broken);
            spline.SetKnot(index, Knot(tr, tr.TransformPoint((Vector3)old.Position), incoming, outgoing));
        }
        private static BezierKnot Knot(Transform tr, Vector3 position, Vector3 incoming, Vector3 outgoing) =>
            new BezierKnot(tr.InverseTransformPoint(position), tr.InverseTransformVector(incoming),
                tr.InverseTransformVector(outgoing), quaternion.identity);
        private static Vector3 Surface(SplineRoad road, Vector3 point) => road.Profile.ConformRoadToTerrain || road.AdjustTerrainToRoad
            ? RoadTerrainHeightUtility.Conform(point, road.Profile.TerrainSurfaceOffset) : point;
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static RoadTurnPlan Fail(RoadTurnPlan plan, string message) { plan.Message = message; return plan; }
    }
}
