using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dyma.SplineLevelToolkit
{
    internal static class RoadFenceMeshGenerator
    {
        internal static readonly string[] RailPartNames =
            { "Rail Back", "Upper Rolled Edge", "Lower Rolled Edge", "Center Rib" };

        private readonly struct Bar
        {
            internal Bar(string name, float horizontal, float height, float width, float thickness)
            {
                Name = name;
                Horizontal = horizontal;
                Height = height;
                Width = width;
                Thickness = thickness;
            }

            internal string Name { get; }
            internal float Horizontal { get; }
            internal float Height { get; }
            internal float Width { get; }
            internal float Thickness { get; }
        }

        private static readonly Bar[] Bars =
        {
            new Bar("Rail Back", 0f, 0.78f, 0.08f, 0.32f),
            new Bar("Upper Rolled Edge", 0.075f, 0.95f, 0.08f, 0.055f),
            new Bar("Lower Rolled Edge", 0.075f, 0.61f, 0.08f, 0.055f),
            new Bar("Center Rib", 0.065f, 0.78f, 0.06f, 0.055f)
        };

        internal static void Build(Transform parent, IReadOnlyList<SplineSamplingUtility.Sample> samples,
            RoadProfile profile, SplinePropLayer layer, float start, float end)
        {
            Material material = layer.Prefab.transform.Find("Rail Back")?.GetComponent<MeshRenderer>()?.sharedMaterial;
            if (material == null)
                return;

            if (layer.Side == PropSide.Both || layer.Side == PropSide.Left)
                BuildSide(parent, samples, profile, layer, start, end, -1f, material);
            if (layer.Side == PropSide.Both || layer.Side == PropSide.Right)
                BuildSide(parent, samples, profile, layer, start, end, 1f, material);
            if (layer.Side == PropSide.Center)
                BuildSide(parent, samples, profile, layer, start, end, 0f, material);
        }

        private static void BuildSide(Transform parent, IReadOnlyList<SplineSamplingUtility.Sample> samples,
            RoadProfile profile, SplinePropLayer layer, float start, float end, float side, Material material)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt((end - start) / 0.4f));
            var positions = new Vector3[steps + 1];
            var rights = new Vector3[steps + 1];
            var distances = new float[steps + 1];
            float lateral = side == 0f ? layer.LateralOffset : side * (profile.Width * 0.5f + layer.LateralOffset);

            for (int i = 0; i <= steps; i++)
            {
                float distance = Mathf.Lerp(start, end, i / (float)steps);
                SplineSamplingUtility.Sample sample = SplineSamplingUtility.EvaluateAtDistance(samples, distance);
                Vector3 right = Vector3.Cross(sample.Up, sample.Tangent).normalized;
                if (right.sqrMagnitude < 0.5f)
                    right = Vector3.right;
                Vector3 position = sample.Position + right * lateral + sample.Up * layer.VerticalOffset;
                if (profile.ConformRoadToTerrain || layer.Grounding == PropGroundingMode.Terrain)
                    position = RoadTerrainHeightUtility.Conform(position, layer.VerticalOffset);
                positions[i] = position;
                rights[i] = right;
                distances[i] = distance - start;
            }

            foreach (Bar bar in Bars)
            {
                Mesh mesh = BuildBarMesh(parent, positions, rights, distances, bar);
                var part = new GameObject("Continuous Fence " + bar.Name);
                part.transform.SetParent(parent, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        private static Mesh BuildBarMesh(Transform parent, Vector3[] positions, Vector3[] rights,
            float[] distances, Bar bar)
        {
            int rings = positions.Length;
            var vertices = new Vector3[rings * 8];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(rings - 1) * 24];

            for (int i = 0; i < rings; i++)
            {
                Vector3 across = rights[i];
                Vector3 center = positions[i] + across * bar.Horizontal + Vector3.up * bar.Height;
                Vector3 a = center - across * (bar.Width * 0.5f) - Vector3.up * (bar.Thickness * 0.5f);
                Vector3 b = center + across * (bar.Width * 0.5f) - Vector3.up * (bar.Thickness * 0.5f);
                Vector3 c = center + across * (bar.Width * 0.5f) + Vector3.up * (bar.Thickness * 0.5f);
                Vector3 d = center - across * (bar.Width * 0.5f) + Vector3.up * (bar.Thickness * 0.5f);
                Vector3[] corners = { a, b, b, c, c, d, d, a };
                for (int j = 0; j < 8; j++)
                {
                    vertices[i * 8 + j] = parent.InverseTransformPoint(corners[j]);
                    uv[i * 8 + j] = new Vector2(distances[i] / 2f, j % 2);
                }
            }

            int index = 0;
            for (int i = 0; i < rings - 1; i++)
            {
                for (int face = 0; face < 4; face++)
                {
                    int current = i * 8 + face * 2;
                    int next = (i + 1) * 8 + face * 2;
                    triangles[index++] = current;
                    triangles[index++] = next;
                    triangles[index++] = current + 1;
                    triangles[index++] = current + 1;
                    triangles[index++] = next;
                    triangles[index++] = next + 1;
                }
            }

            var mesh = new Mesh { name = "Continuous Fence " + bar.Name };
            if (vertices.Length > 65535)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
