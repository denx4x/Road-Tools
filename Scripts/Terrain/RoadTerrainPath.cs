using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit
{
    internal sealed class RoadTerrainPath
    {
        private readonly Dictionary<Vector2Int, List<Segment>> cells = new();
        private readonly float cellSize;

        private readonly struct Segment
        {
            public Segment(Vector3 a, Vector3 b) { A = a; B = b; }
            public Vector3 A { get; }
            public Vector3 B { get; }
        }

        public RoadTerrainPath(SplineContainer container, float spacing, float searchRadius)
        {
            cellSize = Mathf.Max(2f, searchRadius);
            for (int index = 0; index < container.Splines.Count; index++)
            {
                if (container.Splines[index] == null || container.Splines[index].Count < 2)
                    continue;

                List<SplineSamplingUtility.Sample> samples =
                    SplineSamplingUtility.BuildArcLengthSamples(container, index, Mathf.Max(0.25f, spacing));
                for (int sample = 1; sample < samples.Count; sample++)
                    AddSegment(samples[sample - 1].Position, samples[sample].Position);
            }
        }

        public bool TryNearest(Vector3 worldPosition, out float distance, out float roadHeight)
        {
            float bestSquared = float.PositiveInfinity;
            roadHeight = 0f;
            Vector2Int cell = ToCell(worldPosition);
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                if (!cells.TryGetValue(new Vector2Int(cell.x + offsetX, cell.y + offsetZ),
                        out List<Segment> segments))
                    continue;
                foreach (Segment segment in segments)
                {
                    Vector3 a = segment.A;
                    Vector3 b = segment.B;
                    float dx = b.x - a.x;
                    float dz = b.z - a.z;
                    float lengthSquared = dx * dx + dz * dz;
                    float t = lengthSquared < 0.0001f ? 0f : Mathf.Clamp01(
                        ((worldPosition.x - a.x) * dx + (worldPosition.z - a.z) * dz) / lengthSquared);
                    float x = a.x + dx * t - worldPosition.x;
                    float z = a.z + dz * t - worldPosition.z;
                    float squared = x * x + z * z;
                    if (squared >= bestSquared)
                        continue;
                    bestSquared = squared;
                    roadHeight = Mathf.Lerp(a.y, b.y, t);
                }
            }

            distance = Mathf.Sqrt(bestSquared);
            return !float.IsInfinity(bestSquared);
        }

        private void AddSegment(Vector3 a, Vector3 b)
        {
            var segment = new Segment(a, b);
            Vector2Int minimum = ToCell(new Vector3(Mathf.Min(a.x, b.x), 0f, Mathf.Min(a.z, b.z)));
            Vector2Int maximum = ToCell(new Vector3(Mathf.Max(a.x, b.x), 0f, Mathf.Max(a.z, b.z)));
            for (int x = minimum.x; x <= maximum.x; x++)
            for (int z = minimum.y; z <= maximum.y; z++)
            {
                var cell = new Vector2Int(x, z);
                if (!cells.TryGetValue(cell, out List<Segment> segments))
                {
                    segments = new List<Segment>();
                    cells.Add(cell, segments);
                }
                segments.Add(segment);
            }
        }

        private Vector2Int ToCell(Vector3 position) => new(
            Mathf.FloorToInt(position.x / cellSize), Mathf.FloorToInt(position.z / cellSize));
    }
}
