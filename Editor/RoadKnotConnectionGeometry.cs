using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal sealed class RoadKnotConnectionPlan
    {
        internal RoadKnotConnectionReference A;
        internal RoadKnotConnectionReference B;
        internal Spline MergedSpline;
        internal BezierCurve Bridge;
        internal bool Succeeded;
        internal bool Welded;
        internal bool HasBridge;
        internal bool TightBend;
        internal string Message;
        internal int JoinKnotIndex;
        internal Vector3 PositionA;
        internal Vector3 PositionB;
    }

    internal static class RoadKnotConnectionGeometry
    {
        internal static RoadKnotConnectionPlan Plan(RoadKnotConnectionReference a, RoadKnotConnectionReference b)
        {
            var plan = new RoadKnotConnectionPlan { A = a, B = b };
            if (a == null || b == null) return Fail(plan, "Click endpoint A, then endpoint B, or capture them with the buttons below.");
            if (!a.TryResolve(out Spline sourceA, out SplineRoad roadA, out string error)) return Fail(plan, "A: " + error);
            if (!b.TryResolve(out Spline sourceB, out SplineRoad roadB, out error)) return Fail(plan, "B: " + error);
            if (sourceA == sourceB) return Fail(plan, "Choose endpoints from two different splines.");
            if (roadA.IsBaked || roadB.IsBaked) return Fail(plan, "Resume editing both roads before merging.");
            if ((roadA.GetComponent<RoadMaterialSections>()?.Sections.Count ?? 0) > 0 ||
                (roadB.GetComponent<RoadMaterialSections>()?.Sections.Count ?? 0) > 0)
                return Fail(plan, "Remove local material sections before merging, then recreate them on the merged road. Their ranges cannot be remapped during a merge yet.");
            if (!roadA.isActiveAndEnabled || !roadB.isActiveAndEnabled) return Fail(plan, "Enable both road components before merging.");
            if (!roadA.LiveUpdatesEnabled || !roadB.LiveUpdatesEnabled)
                return Fail(plan, "Enable live updates on both roads before merging so Undo can rebuild their mesh, terrain and props.");
            if (a.Container.gameObject.scene != b.Container.gameObject.scene)
                return Fail(plan, "Both roads must belong to the same scene.");
            if (sourceA.Closed || sourceB.Closed) return Fail(plan, "Open both splines before merging; a closed spline has no endpoint.");
            if (sourceA.Count < 2 || sourceB.Count < 2) return Fail(plan, "Each source spline needs at least two points.");
            if (!IsEndpoint(a, sourceA) || !IsEndpoint(b, sourceB))
                return Fail(plan, "Select the first or last knot of each spline. An internal knot forms a branch, which cannot become one continuous spline without splitting it.");
            if (Mathf.Abs(roadA.Profile.Width - roadB.Profile.Width) > 0.01f)
                return Fail(plan, "Road widths differ. Match both profile widths before merging; a width taper is not supported yet.");
            if (HasLinkedKnots(a.Container, a.SplineIndex, sourceA.Count) || HasLinkedKnots(b.Container, b.SplineIndex, sourceB.Count))
                return Fail(plan, "A source spline has linked junction knots. Unlink those junctions before merging so their references stay intact.");
            if (Mathf.Abs(a.Container.transform.localToWorldMatrix.determinant) < 0.00001f ||
                Mathf.Abs(b.Container.transform.localToWorldMatrix.determinant) < 0.00001f)
                return Fail(plan, "A road transform has zero scale. Restore a nonzero scale before merging.");

            bool reverseA = a.KnotIndex == 0;
            bool reverseB = b.KnotIndex != 0;
            Matrix4x4 bToA = a.Container.transform.worldToLocalMatrix * b.Container.transform.localToWorldMatrix;
            var knotsA = CopyKnots(sourceA, Matrix4x4.identity, reverseA);
            var knotsB = CopyKnots(sourceB, bToA, reverseB);
            int lastA = knotsA.Count - 1;
            BezierKnot endA = knotsA[lastA];
            BezierKnot startB = knotsB[0];
            Transform transformA = a.Container.transform;
            plan.PositionA = transformA.TransformPoint((Vector3)endA.Position);
            plan.PositionB = transformA.TransformPoint((Vector3)startB.Position);
            float gap = Vector3.Distance(plan.PositionA, plan.PositionB);
            plan.Welded = gap <= 0.01f;
            plan.JoinKnotIndex = lastA;

            if (plan.Welded)
            {
                Vector3 incoming = WorldTangent(transformA, endA, false).normalized;
                Vector3 outgoing = WorldTangent(transformA, startB, true).normalized;
                if (incoming.sqrMagnitude < 0.5f || outgoing.sqrMagnitude < 0.5f || Vector3.Dot(incoming, outgoing) < 0.999f)
                    return Fail(plan, "Coincident endpoints have different directions. Align their handles before welding to keep the join smooth.");
                // The coincident pair becomes one knot; incoming A and outgoing B curves remain intact.
                endA.TangentOut = startB.TangentOut;
                knotsA[lastA] = endA;
            }
            else
            {
                Vector3 forwardA = WorldTangent(transformA, endA, false);
                Vector3 forwardB = WorldTangent(transformA, startB, true);
                if (forwardA.sqrMagnitude < 0.0001f)
                    forwardA = plan.PositionA - transformA.TransformPoint((Vector3)knotsA[lastA - 1].Position);
                if (forwardB.sqrMagnitude < 0.0001f)
                    forwardB = transformA.TransformPoint((Vector3)knotsB[1].Position) - plan.PositionB;
                if (forwardA.sqrMagnitude < 0.0001f || forwardB.sqrMagnitude < 0.0001f)
                    return Fail(plan, "An endpoint has no usable direction. Separate its neighbouring knot first.");
                forwardA.Normalize(); forwardB.Normalize();
                float handleLength = ChooseHandleLength(plan.PositionA, plan.PositionB, forwardA, forwardB, gap, roadA.Profile.Width, out bool tight);
                endA.TangentOut = (float3)transformA.InverseTransformVector(forwardA * handleLength);
                startB.TangentIn = (float3)transformA.InverseTransformVector(-forwardB * handleLength);
                knotsA[lastA] = endA;
                knotsB[0] = startB;
                plan.HasBridge = true;
                plan.Bridge = new BezierCurve(plan.PositionA, plan.PositionA + forwardA * handleLength,
                    plan.PositionB - forwardB * handleLength, plan.PositionB);
                plan.TightBend = tight;
            }

            var merged = new Spline(knotsA.Count + knotsB.Count);
            foreach (BezierKnot knot in knotsA) merged.Add(knot, TangentMode.Broken);
            for (int i = plan.Welded ? 1 : 0; i < knotsB.Count; i++) merged.Add(knotsB[i], TangentMode.Broken);
            RoadKnotConnectionSplineData.Copy(sourceA, merged, reverseA, 0);
            RoadKnotConnectionSplineData.Copy(sourceB, merged, reverseB, plan.Welded ? lastA : knotsA.Count);
            plan.MergedSpline = merged;
            plan.Succeeded = true;
            plan.Message = $"Merge into {roadA.name}: {merged.Count} points, {gap:0.##} m gap. Uses road A's profile and prop layers. " +
                (plan.Welded ? "Coincident endpoints are welded into one shared knot. " : "All source points are preserved. ") +
                "Bezier handles preserve the existing curves." +
                (plan.TightBend ? " The bridge is tight for this width; move the endpoints farther apart or adjust their directions." : "");
            return plan;
        }

        private static RoadKnotConnectionPlan Fail(RoadKnotConnectionPlan plan, string message)
        { plan.Message = message; return plan; }

        private static bool IsEndpoint(RoadKnotConnectionReference reference, Spline spline) =>
            reference.KnotIndex == 0 || reference.KnotIndex == spline.Count - 1;

        private static bool HasLinkedKnots(SplineContainer container, int spline, int count)
        {
            for (int i = 0; i < count; i++)
                if (container.KnotLinkCollection.TryGetKnotLinks(new SplineKnotIndex(spline, i), out var links) && links.Count > 1) return true;
            return false;
        }

        private static List<BezierKnot> CopyKnots(Spline source, Matrix4x4 matrix, bool reverse)
        {
            var knots = new List<BezierKnot>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                BezierKnot original = source[reverse ? source.Count - 1 - i : i];
                Vector3 tangentIn = (Vector3)math.rotate(original.Rotation, original.TangentIn);
                Vector3 tangentOut = (Vector3)math.rotate(original.Rotation, original.TangentOut);
                // Bake the full affine transform into the handles; do not approximate nonuniform scale with a quaternion.
                knots.Add(new BezierKnot((float3)matrix.MultiplyPoint3x4((Vector3)original.Position),
                    (float3)matrix.MultiplyVector(reverse ? tangentOut : tangentIn),
                    (float3)matrix.MultiplyVector(reverse ? tangentIn : tangentOut), quaternion.identity));
            }
            return knots;
        }

        private static Vector3 WorldTangent(Transform transform, BezierKnot knot, bool outgoingB) =>
            transform.TransformVector(outgoingB ? (Vector3)knot.TangentOut : -(Vector3)knot.TangentIn);

        private static float ChooseHandleLength(Vector3 a, Vector3 b, Vector3 forwardA, Vector3 forwardB,
            float gap, float width, out bool tight)
        {
            float best = gap / 3f;
            float bestRadius = -1f;
            for (int candidate = 0; candidate < 12; candidate++)
            {
                float length = gap * Mathf.Lerp(0.18f, 1f, candidate / 11f);
                var curve = new BezierCurve(a, a + forwardA * length, b - forwardB * length, b);
                float radius = MinimumRadius(curve);
                if (radius <= bestRadius) continue;
                bestRadius = radius;
                best = length;
            }
            tight = bestRadius < width * 0.5f + 0.5f;
            return best;
        }

        private static float MinimumRadius(BezierCurve curve)
        {
            float result = float.PositiveInfinity;
            Vector3 a = curve.P0, b = curve.P1, c = curve.P2, d = curve.P3;
            Vector3 previousDirection = (b - a).normalized;
            for (int i = 0; i <= 64; i++)
            {
                float t = i / 64f, u = 1f - t;
                Vector3 velocity = 3f * (u * u * (b - a) + 2f * u * t * (c - b) + t * t * (d - c));
                Vector3 acceleration = 6f * (u * (c - 2f * b + a) + t * (d - 2f * c + b));
                float speed = velocity.magnitude;
                if (speed < 0.0001f) return 0f;
                Vector3 direction = velocity / speed;
                // Collinear foldbacks have zero cross product and therefore an apparent infinite radius.
                // A velocity reversal across consecutive samples detects that cusp even between samples.
                if (Vector3.Dot(previousDirection, direction) <= 0f) return 0f;
                previousDirection = direction;
                float cross = Vector3.Cross(velocity, acceleration).magnitude;
                if (cross > 0.00001f) result = Mathf.Min(result, speed * speed * speed / cross);
            }
            return result;
        }
    }
}
