using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal readonly struct RoadSplineRepairResult
    {
        public RoadSplineRepairResult(Spline spline, int adjustedKnots, int smoothedKnots,
            float beforeScore, float afterScore, bool unresolved)
        {
            Spline = spline;
            AdjustedKnots = adjustedKnots;
            SmoothedKnots = smoothedKnots;
            BeforeScore = beforeScore;
            AfterScore = afterScore;
            Unresolved = unresolved;
        }

        public Spline Spline { get; }
        public int AdjustedKnots { get; }
        public int SmoothedKnots { get; }
        public float BeforeScore { get; }
        public float AfterScore { get; }
        public bool Unresolved { get; }
    }

    // Detached candidates keep edits transactional. Preserve knot identities/count, world
    // heights and open endpoints; apply a candidate only when measured geometry improves.
    internal static class RoadSplineRepairUtility
    {
        private const int MaximumEvaluations = 160;
        private static readonly float[] Tensions = { 0.25f, 0.4f, 0.6f };

        public static RoadSplineRepairResult Repair(Spline source, Transform transform, float roadWidth)
        {
            if (source == null || source.Count < 3 || transform == null)
                return new RoadSplineRepairResult(source, 0, 0, 0f, 0f, false);

            roadWidth = Mathf.Max(0.1f, roadWidth);
            int sampleCount = Mathf.Clamp(source.Count * 32, 160, 512);
            var originalPositions = new Vector3[source.Count];
            for (int i = 0; i < source.Count; i++)
                originalPositions[i] = transform.TransformPoint((Vector3)source[i].Position);

            Spline best = Clone(source);
            float beforeScore = EvaluateScore(best, transform, roadWidth, sampleCount, out bool unresolved);
            if (!unresolved)
                return new RoadSplineRepairResult(best, 0, 0, beforeScore, beforeScore, false);

            float bestScore = beforeScore;
            int evaluations = 0;
            float movementLimit = roadWidth;

            foreach (float tension in Tensions)
            {
                Spline candidate = Clone(best);
                SmoothAll(candidate, tension);
                TryAccept(candidate, transform, roadWidth, sampleCount, ref best, ref bestScore, ref evaluations);
            }

            // Adjacent short/zigzag segments can improve together. Every knot stays within
            // one road width of its authored position and retains its original world Y.
            for (int pass = 0; pass < 6 && evaluations < MaximumEvaluations && bestScore > 0.0001f; pass++)
            {
                float passScore = bestScore;
                foreach (float amount in new[] { 0.25f, 0.5f })
                {
                    Spline candidate = Clone(best);
                    for (int i = FirstEditable(best); i <= LastEditable(best); i++)
                    {
                        Vector3 previous = World(best, transform, Previous(best, i));
                        Vector3 next = World(best, transform, Next(best, i));
                        Vector3 position = Vector3.Lerp(World(best, transform, i), (previous + next) * 0.5f, amount);
                        SetPosition(candidate, transform, i, ClampPosition(position, originalPositions[i], movementLimit));
                    }
                    SmoothAll(candidate, 0.4f);
                    TryAccept(candidate, transform, roadWidth, sampleCount, ref best, ref bestScore, ref evaluations);
                }

                // Worst bends/short segments first; bound the explicit action on long roads.
                List<int> indices = OrderedEditableKnots(best, transform, roadWidth);
                foreach (int i in indices)
                {
                    if (evaluations >= MaximumEvaluations || bestScore <= 0.0001f)
                        break;
                    Vector3 current = World(best, transform, i);
                    Vector3 previous = World(best, transform, Previous(best, i));
                    Vector3 next = World(best, transform, Next(best, i));
                    Vector3 midpoint = (previous + next) * 0.5f;
                    Vector3 forward = Vector3.ProjectOnPlane(next - previous, Vector3.up).normalized;
                    if (forward.sqrMagnitude < 0.5f)
                        forward = Vector3.ProjectOnPlane(current - previous, Vector3.up).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    float step = roadWidth * (pass < 3 ? 0.2f : 0.1f);
                    var positions = new[]
                    {
                        Vector3.Lerp(current, midpoint, 0.4f),
                        current + right * step,
                        current - right * step,
                        current + forward * step,
                        current - forward * step
                    };
                    foreach (Vector3 position in positions)
                    {
                        if (evaluations >= MaximumEvaluations) break;
                        Spline candidate = Clone(best);
                        SetPosition(candidate, transform, i, ClampPosition(position, originalPositions[i], movementLimit));
                        SmoothAround(candidate, i, 0.4f);
                        TryAccept(candidate, transform, roadWidth, sampleCount, ref best, ref bestScore, ref evaluations);
                    }
                    foreach (float tension in Tensions)
                    {
                        if (evaluations >= MaximumEvaluations) break;
                        Spline candidate = Clone(best);
                        SmoothAround(candidate, i, tension);
                        TryAccept(candidate, transform, roadWidth, sampleCount, ref best, ref bestScore, ref evaluations);
                    }
                }
                if (bestScore >= passScore - 0.00001f) break;
            }

            EvaluateScore(best, transform, roadWidth, sampleCount, out unresolved);
            int adjusted = 0;
            int smoothed = 0;
            for (int i = 0; i < source.Count; i++)
            {
                bool tangentChanged = source.GetTangentMode(i) != best.GetTangentMode(i) ||
                    math.distancesq(source[i].TangentIn, best[i].TangentIn) > 0.000001f ||
                    math.distancesq(source[i].TangentOut, best[i].TangentOut) > 0.000001f ||
                    math.abs(math.dot(source[i].Rotation.value, best[i].Rotation.value)) < 0.999999f;
                if (tangentChanged) smoothed++;
                if (tangentChanged || math.distancesq(source[i].Position, best[i].Position) > 0.000001f)
                    adjusted++;
            }
            return new RoadSplineRepairResult(best, adjusted, smoothed, beforeScore, bestScore, unresolved);
        }

        public static float EvaluateScore(Spline spline, Transform transform, float roadWidth, int sampleCount,
            out bool unresolved)
        {
            List<SplineSamplingUtility.Sample> samples = BuildSamples(spline, transform, sampleCount);
            float score = 0f;
            int tight = RoadCornerWidthUtility.CountTightCorners(samples, roadWidth);
            float[] maximumWidths = RoadSelfClearanceUtility.BuildMaximumWidths(samples, roadWidth);
            unresolved = tight > 0 || RoadSelfClearanceUtility.HasWidthReduction(maximumWidths, roadWidth);
            float desiredRadius = roadWidth * 0.6f;
            for (int i = 1; i < samples.Count - 1; i++)
            {
                Vector3 before = Vector3.ProjectOnPlane(samples[i - 1].Tangent, Vector3.up).normalized;
                Vector3 after = Vector3.ProjectOnPlane(samples[i + 1].Tangent, Vector3.up).normalized;
                float angle = Vector3.Angle(before, after) * Mathf.Deg2Rad;
                float span = samples[i + 1].Distance - samples[i - 1].Distance;
                if (angle > 0.01f && span > 0.00001f)
                {
                    float curvatureDeficit = Mathf.Max(0f, desiredRadius * angle / span - 1f);
                    score += Mathf.Min(25f, curvatureDeficit * curvatureDeficit) * 4f;
                }
                float clearanceDeficit = 1f - Mathf.Clamp01(maximumWidths[i] / roadWidth);
                score += clearanceDeficit * clearanceDeficit * 8f;
            }
            int segmentCount = spline.Closed ? spline.Count : spline.Count - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                float distance = Vector3.Distance(World(spline, transform, i), World(spline, transform, Next(spline, i)));
                float deficit = 1f - Mathf.Clamp01(distance / (roadWidth * 0.35f));
                score += deficit * deficit * sampleCount / Mathf.Max(1, segmentCount);
                if (distance < 0.001f) unresolved = true;
            }
            return score / sampleCount;
        }

        private static void TryAccept(Spline candidate, Transform transform, float width, int count,
            ref Spline best, ref float bestScore, ref int evaluations)
        {
            evaluations++;
            float score = EvaluateScore(candidate, transform, width, count, out _);
            if (float.IsNaN(score) || float.IsInfinity(score) || score >= bestScore - 0.00001f) return;
            best = candidate;
            bestScore = score;
        }

        private static Spline Clone(Spline source)
        {
            // This package's copy constructor copies knots, not tangent metadata.
            var clone = new Spline(source);
            for (int i = 0; i < source.Count; i++)
            {
                clone.SetTangentModeNoNotify(i, source.GetTangentMode(i));
                clone.SetAutoSmoothTensionNoNotify(i, source.GetAutoSmoothTension(i));
                clone.SetKnotNoNotify(i, source[i]);
            }
            return clone;
        }

        private static List<SplineSamplingUtility.Sample> BuildSamples(Spline spline, Transform transform, int count)
        {
            var samples = new List<SplineSamplingUtility.Sample>(count);
            float distance = 0f;
            Vector3 previous = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                Vector3 position = transform.TransformPoint((Vector3)SplineUtility.EvaluatePosition(spline, t));
                Vector3 tangent = transform.TransformVector((Vector3)SplineUtility.EvaluateTangent(spline, t)).normalized;
                if (i > 0) distance += Vector3.Distance(previous, position);
                samples.Add(new SplineSamplingUtility.Sample(t, position, tangent, Vector3.up, distance));
                previous = position;
            }
            return samples;
        }

        private static List<int> OrderedEditableKnots(Spline spline, Transform transform, float width)
        {
            var priorities = new Dictionary<int, float>();
            var indices = new List<int>();
            for (int i = FirstEditable(spline); i <= LastEditable(spline); i++)
            {
                Vector3 incoming = World(spline, transform, i) - World(spline, transform, Previous(spline, i));
                Vector3 outgoing = World(spline, transform, Next(spline, i)) - World(spline, transform, i);
                priorities[i] = Vector3.Angle(incoming, outgoing) +
                    90f * (1f - Mathf.Clamp01(Mathf.Min(incoming.magnitude, outgoing.magnitude) / width));
                indices.Add(i);
            }
            indices.Sort((a, b) =>
            {
                int order = priorities[b].CompareTo(priorities[a]);
                return order == 0 ? a.CompareTo(b) : order;
            });
            return indices;
        }

        private static void SmoothAll(Spline spline, float tension)
        {
            for (int i = 0; i < spline.Count; i++) Smooth(spline, i, tension);
        }

        private static void SmoothAround(Spline spline, int i, float tension)
        {
            Smooth(spline, Previous(spline, i), tension);
            Smooth(spline, i, tension);
            Smooth(spline, Next(spline, i), tension);
        }

        private static void Smooth(Spline spline, int i, float tension)
        {
            spline.SetTangentModeNoNotify(i, TangentMode.AutoSmooth);
            spline.SetAutoSmoothTensionNoNotify(i, tension);
        }

        private static void SetPosition(Spline spline, Transform transform, int i, Vector3 worldPosition)
        {
            BezierKnot knot = spline[i];
            knot.Position = (float3)transform.InverseTransformPoint(worldPosition);
            spline.SetKnotNoNotify(i, knot);
        }

        private static Vector3 ClampPosition(Vector3 position, Vector3 original, float limit)
        {
            position.y = original.y;
            return original + Vector3.ClampMagnitude(position - original, limit);
        }

        private static Vector3 World(Spline spline, Transform transform, int i) =>
            transform.TransformPoint((Vector3)spline[i].Position);
        private static int FirstEditable(Spline spline) => spline.Closed ? 0 : 1;
        private static int LastEditable(Spline spline) => spline.Closed ? spline.Count - 1 : spline.Count - 2;
        private static int Previous(Spline spline, int i) => spline.Closed ? (i + spline.Count - 1) % spline.Count : Mathf.Max(0, i - 1);
        private static int Next(Spline spline, int i) => spline.Closed ? (i + 1) % spline.Count : Mathf.Min(spline.Count - 1, i + 1);
    }
}
