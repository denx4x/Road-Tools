using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit
{
    public static class SplineSamplingUtility
    {
        public readonly struct Sample
        {
            public Sample(float t, Vector3 position, Vector3 tangent, Vector3 up, float distance)
            {
                T = t;
                Position = position;
                Tangent = tangent;
                Up = up;
                Distance = distance;
            }

            public float T { get; }
            public Vector3 Position { get; }
            public Vector3 Tangent { get; }
            public Vector3 Up { get; }
            public float Distance { get; }
        }

        public static List<Sample> BuildArcLengthSamples(SplineContainer container, float spacing)
        {
            return BuildArcLengthSamples(container, 0, spacing);
        }

        public static List<Sample> BuildArcLengthSamples(SplineContainer container, int splineIndex, float spacing)
        {
            if (container == null || splineIndex < 0 || splineIndex >= container.Splines.Count)
                return new List<Sample>();

            var preview = BuildUniformSamples(container, splineIndex, 64);
            float length = preview.Count > 0 ? preview[preview.Count - 1].Distance : 0f;
            int count = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(0.1f, spacing)) + 1);
            return BuildUniformSamples(container, splineIndex, count);
        }

        public static Sample EvaluateAtDistance(IReadOnlyList<Sample> samples, float distance)
        {
            if (samples == null || samples.Count == 0)
                return default;
            if (samples.Count == 1)
                return samples[0];

            distance = Mathf.Clamp(distance, 0f, samples[samples.Count - 1].Distance);
            int low = 1;
            int high = samples.Count - 1;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (samples[middle].Distance < distance)
                    low = middle + 1;
                else
                    high = middle;
            }

            Sample a = samples[low - 1];
            Sample b = samples[low];
            float span = Mathf.Max(0.0001f, b.Distance - a.Distance);
            float t = Mathf.Clamp01((distance - a.Distance) / span);
            return new Sample(
                Mathf.Lerp(a.T, b.T, t),
                Vector3.Lerp(a.Position, b.Position, t),
                Vector3.Slerp(a.Tangent, b.Tangent, t).normalized,
                Vector3.Slerp(a.Up, b.Up, t).normalized,
                distance);
        }

        private static List<Sample> BuildUniformSamples(SplineContainer container, int splineIndex, int count)
        {
            var samples = new List<Sample>(count);
            float distance = 0f;
            Vector3 previous = Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : i / (float)(count - 1);
                container.Evaluate(splineIndex, t, out float3 position, out float3 tangent, out float3 up);
                Vector3 worldPosition = position;
                Vector3 worldTangent = ((Vector3)tangent).normalized;
                // Banking is intentionally deferred in v0.1. A stable world-up basis avoids
                // ribbon flips when knots do not carry authored rotations.
                Vector3 worldUp = Vector3.up;
                if (i > 0)
                    distance += Vector3.Distance(previous, worldPosition);

                samples.Add(new Sample(t, worldPosition, worldTangent, worldUp, distance));
                previous = worldPosition;
            }

            return samples;
        }
    }
}
