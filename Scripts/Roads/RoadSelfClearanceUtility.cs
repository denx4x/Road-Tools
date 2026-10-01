using System.Collections.Generic;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    public static class RoadSelfClearanceUtility
    {
        private const float MinimumArcSeparationMultiplier = 1.5f;
        private const float ClearanceSafetyFactor = 0.85f;
        private const float MinimumRoadWidth = 0.1f;

        public static float[] BuildMaximumWidths(
            IReadOnlyList<SplineSamplingUtility.Sample> samples,
            float roadWidth)
        {
            if (samples == null)
                return new float[0];

            var maximumWidths = new float[samples.Count];
            float cellSize = Mathf.Max(0.5f, roadWidth);
            float widthSquared = roadWidth * roadWidth;
            float minimumArcSeparation = roadWidth * MinimumArcSeparationMultiplier;
            var cells = new Dictionary<Vector3Int, List<int>>();

            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 position = samples[i].Position;
                Vector3Int cell = ToCell(position, cellSize);
                if (!cells.TryGetValue(cell, out List<int> indices))
                {
                    indices = new List<int>();
                    cells.Add(cell, indices);
                }

                indices.Add(i);
            }

            for (int i = 0; i < samples.Count; i++)
            {
                SplineSamplingUtility.Sample sample = samples[i];
                Vector3Int cell = ToCell(sample.Position, cellSize);
                float nearestDistanceSquared = widthSquared;

                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    var neighborCell = new Vector3Int(cell.x + x, cell.y + y, cell.z + z);
                    if (!cells.TryGetValue(neighborCell, out List<int> indices))
                        continue;

                    for (int candidate = 0; candidate < indices.Count; candidate++)
                    {
                        int otherIndex = indices[candidate];
                        if (Mathf.Abs(samples[otherIndex].Distance - sample.Distance) < minimumArcSeparation)
                            continue;

                        float distanceSquared = (samples[otherIndex].Position - sample.Position).sqrMagnitude;
                        if (distanceSquared < nearestDistanceSquared)
                            nearestDistanceSquared = distanceSquared;
                    }
                }

                float nearestDistance = Mathf.Sqrt(nearestDistanceSquared);
                maximumWidths[i] = nearestDistanceSquared < widthSquared
                    ? Mathf.Max(MinimumRoadWidth, nearestDistance * ClearanceSafetyFactor)
                    : roadWidth;
            }

            return maximumWidths;
        }

        public static bool HasWidthReduction(IReadOnlyList<float> maximumWidths, float roadWidth)
        {
            if (maximumWidths == null)
                return false;

            for (int i = 0; i < maximumWidths.Count; i++)
            {
                if (maximumWidths[i] < roadWidth - 0.01f)
                    return true;
            }

            return false;
        }

        public static int CountWidthReductions(IReadOnlyList<float> maximumWidths, float roadWidth)
        {
            if (maximumWidths == null)
                return 0;

            int count = 0;
            for (int i = 0; i < maximumWidths.Count; i++)
            {
                if (maximumWidths[i] < roadWidth - 0.01f)
                    count++;
            }

            return count;
        }

        public static float EvaluateMaximumWidth(
            IReadOnlyList<SplineSamplingUtility.Sample> samples,
            IReadOnlyList<float> maximumWidths,
            float distance)
        {
            if (samples == null || maximumWidths == null || samples.Count == 0 || maximumWidths.Count == 0)
                return float.PositiveInfinity;
            if (samples.Count == 1 || maximumWidths.Count == 1)
                return maximumWidths[0];

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

            int before = low - 1;
            float span = Mathf.Max(0.0001f, samples[low].Distance - samples[before].Distance);
            float t = Mathf.Clamp01((distance - samples[before].Distance) / span);
            return Mathf.Lerp(maximumWidths[before], maximumWidths[low], t);
        }

        private static Vector3Int ToCell(Vector3 position, float cellSize)
        {
            return new Vector3Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.y / cellSize),
                Mathf.FloorToInt(position.z / cellSize));
        }
    }
}
