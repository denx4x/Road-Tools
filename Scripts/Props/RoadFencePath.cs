using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    /// <summary>Shares a smooth, upright fence axis across every part of an imported segment.</summary>
    internal sealed class RoadFencePath
    {
        private const float MaximumSpacing = 0.2f;
        private const float DerivativeStep = 0.05f;

        private readonly IReadOnlyList<SplineSamplingUtility.Sample> samples;
        private readonly float lateral;
        private readonly float verticalOffset;
        private readonly bool conformTerrain;
        private readonly float length;
        private readonly float cacheStart;
        private readonly float spacing;
        private readonly float[] groundHeights;
        private readonly float[] smoothHeights;
        private readonly bool[] hasTerrain;

        internal float Start => cacheStart;
        internal float End => cacheStart + spacing * (smoothHeights.Length - 1);

        private RoadFencePath(IReadOnlyList<SplineSamplingUtility.Sample> samples, float lateral,
            float start, float end, bool conformTerrain, float verticalOffset, float smoothingDistance)
        {
            this.samples = samples ?? Array.Empty<SplineSamplingUtility.Sample>();
            this.lateral = lateral;
            this.verticalOffset = verticalOffset;
            this.conformTerrain = conformTerrain;
            length = this.samples.Count > 1 ? Mathf.Max(0f, this.samples[this.samples.Count - 1].Distance) : 0f;

            float width = IsFinite(smoothingDistance) ? Mathf.Max(0f, smoothingDistance) : 0f;
            // A radius larger than the full path adds only endpoint extrapolation work.
            width = Mathf.Min(width, Mathf.Max(MaximumSpacing, length * 2f));
            float radius = width * 0.5f;
            float first = Mathf.Clamp(Mathf.Min(start, end), 0f, length);
            float last = Mathf.Clamp(Mathf.Max(start, end), first, length);
            float padding = radius + MaximumSpacing;
            cacheStart = Mathf.Max(0f, first - padding);
            float cacheEnd = Mathf.Min(length, last + padding);
            float span = cacheEnd - cacheStart;
            int intervals = Mathf.Max(1, Mathf.CeilToInt(span / MaximumSpacing));
            spacing = span > 0.00001f ? span / intervals : MaximumSpacing;
            int count = span > 0.00001f ? intervals + 1 : 1;
            groundHeights = new float[count];
            smoothHeights = new float[count];
            hasTerrain = new bool[count];

            for (int i = 0; i < count; i++)
            {
                Vector3 position = AxisPosition(cacheStart + i * spacing, out _);
                float height = 0f;
                hasTerrain[i] = conformTerrain && RoadTerrainHeightUtility.TrySampleHeight(position, out height);
                groundHeights[i] = hasTerrain[i] ? height + verticalOffset : position.y;
                smoothHeights[i] = groundHeights[i];
            }

            if (radius > spacing * 0.5f && count > 1 && conformTerrain)
                SmoothTerrainHeight(radius, width * 0.25f);
        }

        public static RoadFencePath Build(IReadOnlyList<SplineSamplingUtility.Sample> samples, float lateral,
            float start, float end, bool conformTerrain, float verticalOffset, float smoothingDistance)
        {
            return new RoadFencePath(samples, lateral, start, end, conformTerrain, verticalOffset, smoothingDistance);
        }

        public void Evaluate(float distance, out Vector3 position, out Vector3 right, out Vector3 derivative)
        {
            distance = Mathf.Clamp(distance, 0f, length);
            position = PositionAt(distance, out right);
            float before = Mathf.Max(0f, distance - DerivativeStep);
            float after = Mathf.Min(length, distance + DerivativeStep);
            derivative = after - before > 0.00001f
                ? (PositionAt(after, out _) - PositionAt(before, out _)) / (after - before)
                : Vector3.Cross(right, Vector3.up);
            if (derivative.sqrMagnitude < 0.00000001f)
                derivative = Vector3.Cross(right, Vector3.up);
        }

        /// <summary>Returns the unsmoothed side-axis ground height, including the configured offset.</summary>
        public float GroundHeight(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, length);
            Vector3 position = AxisPosition(distance, out _);
            if (conformTerrain && RoadTerrainHeightUtility.TrySampleHeight(position, out float height))
                return height + verticalOffset;
            // Missing ground must not move support vertices away from their shared rail frame.
            return PositionAt(distance, out _).y;
        }

        private Vector3 PositionAt(float distance, out Vector3 right)
        {
            Vector3 position = AxisPosition(distance, out right);
            if (!conformTerrain)
                return position;
            float coordinate = (distance - cacheStart) / spacing;
            if (coordinate < 0f || coordinate > smoothHeights.Length - 1)
            {
                position.y = SampleGroundHeight(position);
                return position;
            }
            if (smoothHeights.Length == 1)
            {
                position.y = smoothHeights[0];
                return position;
            }

            int index = Mathf.Min(Mathf.FloorToInt(coordinate), smoothHeights.Length - 2);
            float t = coordinate - index;
            // Never interpolate terrain height across an uncovered portion of the road.
            if (!hasTerrain[index] || !hasTerrain[index + 1])
            {
                position.y = SampleGroundHeight(position);
                return position;
            }

            float a = smoothHeights[index];
            float b = smoothHeights[index + 1];
            float slopeA = HeightSlope(index);
            float slopeB = HeightSlope(index + 1);
            float t2 = t * t;
            float t3 = t2 * t;
            position.y = (2f * t3 - 3f * t2 + 1f) * a + (t3 - 2f * t2 + t) * slopeA
                + (-2f * t3 + 3f * t2) * b + (t3 - t2) * slopeB;
            return position;
        }

        private float SampleGroundHeight(Vector3 position)
        {
            return RoadTerrainHeightUtility.TrySampleHeight(position, out float height)
                ? height + verticalOffset : position.y;
        }

        private Vector3 AxisPosition(float distance, out Vector3 right)
        {
            SplineSamplingUtility.Sample sample = SplineSamplingUtility.EvaluateAtDistance(samples, distance);
            Vector3 tangent = sample.Tangent;
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.00000001f)
                tangent = Vector3.forward;
            right = Vector3.Cross(Vector3.up, tangent).normalized;
            return sample.Position + right * lateral + Vector3.up * verticalOffset;
        }

        private float HeightSlope(int index)
        {
            int before = index > 0 && hasTerrain[index - 1] ? index - 1 : index;
            int after = index + 1 < smoothHeights.Length && hasTerrain[index + 1] ? index + 1 : index;
            return after > before ? (smoothHeights[after] - smoothHeights[before]) / (after - before) : 0f;
        }

        private void SmoothTerrainHeight(float radius, float sigma)
        {
            int kernelRadius = Mathf.CeilToInt(radius / spacing);
            var weights = new float[kernelRadius + 1];
            for (int i = 0; i <= kernelRadius; i++)
            {
                float offset = i * spacing;
                weights[i] = offset <= radius ? Mathf.Exp(-0.5f * offset * offset / (sigma * sigma)) : 0f;
            }

            int begin = 0;
            while (begin < groundHeights.Length)
            {
                if (!hasTerrain[begin]) { begin++; continue; }
                int end = begin;
                while (end + 1 < groundHeights.Length && hasTerrain[end + 1])
                    end++;

                float firstSlope = end > begin ? groundHeights[begin + 1] - groundHeights[begin] : 0f;
                float lastSlope = end > begin ? groundHeights[end] - groundHeights[end - 1] : 0f;
                for (int index = begin; index <= end; index++)
                {
                    float total = 0f;
                    float weightSum = 0f;
                    for (int offset = -kernelRadius; offset <= kernelRadius; offset++)
                    {
                        float weight = weights[Mathf.Abs(offset)];
                        int sampleIndex = index + offset;
                        // Linear extension preserves a hill's slope at terrain and road endpoints.
                        float height = sampleIndex < begin ? groundHeights[begin] + (sampleIndex - begin) * firstSlope
                            : sampleIndex > end ? groundHeights[end] + (sampleIndex - end) * lastSlope
                            : groundHeights[sampleIndex];
                        total += height * weight;
                        weightSum += weight;
                    }
                    smoothHeights[index] = total / weightSum;
                }
                begin = end + 1;
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
