using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit
{
    [DisallowMultipleComponent]
    public sealed class RoadMeshGenerator : MonoBehaviour
    {
        private const string GeneratedRootName = "Generated Road Mesh";
        private const float CornerAnalysisSampleSpacing = 0.2f;
        private readonly List<Mesh> generatedMeshes = new();

        public int LastSelfOverlapReductionCount { get; private set; }
        public int LastGeneratedMeshCount { get; private set; }
        public int LastGeneratedVertexCount { get; private set; }

        public void Rebuild(SplineContainer spline, RoadProfile profile)
        {
            LastSelfOverlapReductionCount = 0;
            LastGeneratedMeshCount = 0;
            LastGeneratedVertexCount = 0;
            ClearGenerated();
            if (spline == null || profile == null || spline.Splines.Count == 0)
                return;

            var root = new GameObject(GeneratedRootName);
            root.transform.SetParent(transform, false);
            for (int splineIndex = 0; splineIndex < spline.Splines.Count; splineIndex++)
            {
                if (spline.Splines[splineIndex] == null || spline.Splines[splineIndex].Count < 2)
                    continue;

                List<SplineSamplingUtility.Sample> samples =
                    SplineSamplingUtility.BuildArcLengthSamples(spline, splineIndex, profile.SampleSpacing);
                if (samples.Count < 2)
                    continue;

                float cornerSampleSpacing = Mathf.Min(profile.SampleSpacing, CornerAnalysisSampleSpacing);
                List<SplineSamplingUtility.Sample> cornerSamples = cornerSampleSpacing < profile.SampleSpacing
                    ? SplineSamplingUtility.BuildArcLengthSamples(spline, splineIndex, cornerSampleSpacing)
                    : samples;
                bool hasTightCorner = RoadCornerWidthUtility.HasTightCorner(cornerSamples, profile.Width);
                float[] maximumWidths = RoadSelfClearanceUtility.BuildMaximumWidths(cornerSamples, profile.Width);
                bool hasSelfOverlapRisk = RoadSelfClearanceUtility.HasWidthReduction(maximumWidths, profile.Width);
                LastSelfOverlapReductionCount +=
                    RoadSelfClearanceUtility.CountWidthReductions(maximumWidths, profile.Width);
                bool needsDenseSamples = hasTightCorner || hasSelfOverlapRisk;
                IReadOnlyList<SplineSamplingUtility.Sample> meshSamples = needsDenseSamples ? cornerSamples : samples;
                float meshSampleSpacing = needsDenseSamples ? cornerSampleSpacing : profile.SampleSpacing;

                float totalLength = meshSamples[meshSamples.Count - 1].Distance;
                int chunkCount = Mathf.Max(1, Mathf.CeilToInt(totalLength / Mathf.Max(1f, profile.ChunkLength)));
                var chunkFilters = new List<MeshFilter>(chunkCount);
                for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
                {
                    float start = chunkIndex * profile.ChunkLength;
                    float end = Mathf.Min(totalLength, (chunkIndex + 1) * profile.ChunkLength);
                    chunkFilters.Add(BuildChunk(
                        root.transform,
                        meshSamples,
                        cornerSamples,
                        maximumWidths,
                        meshSampleSpacing,
                        profile,
                        start,
                        end,
                        splineIndex,
                        chunkIndex));
                }
                StitchChunkNormals(chunkFilters);
            }
        }

        public void ClearGenerated()
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null)
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

            ReleaseGeneratedMeshes();
        }

        public void ReleaseGeneratedMeshes()
        {
            foreach (Mesh mesh in generatedMeshes)
            {
                if (mesh == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(mesh);
                else
                    DestroyImmediate(mesh);
            }

            generatedMeshes.Clear();
        }

        private MeshFilter BuildChunk(
            Transform parent,
            IReadOnlyList<SplineSamplingUtility.Sample> samples,
            IReadOnlyList<SplineSamplingUtility.Sample> cornerSamples,
            IReadOnlyList<float> maximumWidths,
            float meshSampleSpacing,
            RoadProfile profile,
            float start,
            float end,
            int splineIndex,
            int chunkIndex)
        {
            int rowCount = Mathf.Max(2, Mathf.CeilToInt((end - start) / meshSampleSpacing) + 1);
            var vertices = new Vector3[rowCount * 4];
            var uvs = new Vector2[rowCount * 4];
            var triangles = new int[(rowCount - 1) * 24 + 12];
            float thickness = Mathf.Max(0.01f, profile.Thickness);

            for (int row = 0; row < rowCount; row++)
            {
                float distance = Mathf.Lerp(start, end, row / (float)(rowCount - 1));
                SplineSamplingUtility.Sample sample = SplineSamplingUtility.EvaluateAtDistance(samples, distance);
                Vector3 right = Vector3.Cross(sample.Up, sample.Tangent).normalized;
                if (right.sqrMagnitude < 0.5f)
                    right = Vector3.right;
                RoadCornerWidthUtility.EvaluateSideWidths(
                    cornerSamples,
                    distance,
                    profile.Width,
                    out float leftWidth,
                    out float rightWidth);
                float maximumWidth = RoadSelfClearanceUtility.EvaluateMaximumWidth(
                    cornerSamples,
                    maximumWidths,
                    distance);
                float currentWidth = leftWidth + rightWidth;
                if (currentWidth > maximumWidth)
                {
                    float widthScale = maximumWidth / currentWidth;
                    leftWidth *= widthScale;
                    rightWidth *= widthScale;
                }

                Vector3 leftPoint = sample.Position - right * leftWidth;
                Vector3 rightPoint = sample.Position + right * rightWidth;
                if (profile.ConformRoadToTerrain)
                {
                    leftPoint = RoadTerrainHeightUtility.Conform(leftPoint, profile.TerrainSurfaceOffset);
                    rightPoint = RoadTerrainHeightUtility.Conform(rightPoint, profile.TerrainSurfaceOffset);
                    RoadTerrainSurfaceFitter.LiftSection(
                        ref leftPoint, ref rightPoint,
                        Mathf.Max(0.05f, profile.TerrainSurfaceOffset));
                }
                int vertex = row * 4;
                Vector3 bottomOffset = sample.Up * thickness;
                vertices[vertex] = transform.InverseTransformPoint(leftPoint);
                vertices[vertex + 1] = transform.InverseTransformPoint(rightPoint);
                vertices[vertex + 2] = transform.InverseTransformPoint(leftPoint - bottomOffset);
                vertices[vertex + 3] = transform.InverseTransformPoint(rightPoint - bottomOffset);
                float v = distance / Mathf.Max(0.01f, profile.UvMetersPerTile);
                uvs[vertex] = new Vector2(0f, v);
                uvs[vertex + 1] = new Vector2(1f, v);
                uvs[vertex + 2] = new Vector2(0f, v);
                uvs[vertex + 3] = new Vector2(1f, v);

                if (row == rowCount - 1)
                    continue;

                int next = vertex + 4;
                int triangle = row * 24;

                // Road surface, underside, and both longitudinal side walls.
                triangles[triangle] = vertex;
                triangles[triangle + 1] = next;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = next;
                triangles[triangle + 5] = next + 1;

                triangles[triangle + 6] = vertex + 2;
                triangles[triangle + 7] = vertex + 3;
                triangles[triangle + 8] = next + 2;
                triangles[triangle + 9] = vertex + 3;
                triangles[triangle + 10] = next + 3;
                triangles[triangle + 11] = next + 2;

                triangles[triangle + 12] = vertex;
                triangles[triangle + 13] = vertex + 2;
                triangles[triangle + 14] = next;
                triangles[triangle + 15] = vertex + 2;
                triangles[triangle + 16] = next + 2;
                triangles[triangle + 17] = next;

                triangles[triangle + 18] = vertex + 1;
                triangles[triangle + 19] = next + 1;
                triangles[triangle + 20] = vertex + 3;
                triangles[triangle + 21] = vertex + 3;
                triangles[triangle + 22] = next + 1;
                triangles[triangle + 23] = next + 3;
            }

            int endCap = triangles.Length - 12;
            triangles[endCap] = 0;
            triangles[endCap + 1] = 1;
            triangles[endCap + 2] = 2;
            triangles[endCap + 3] = 1;
            triangles[endCap + 4] = 3;
            triangles[endCap + 5] = 2;

            int endVertex = (rowCount - 1) * 4;
            triangles[endCap + 6] = endVertex;
            triangles[endCap + 7] = endVertex + 2;
            triangles[endCap + 8] = endVertex + 1;
            triangles[endCap + 9] = endVertex + 1;
            triangles[endCap + 10] = endVertex + 2;
            triangles[endCap + 11] = endVertex + 3;

            var mesh = new Mesh { name = $"Road Spline {splineIndex + 1} Chunk {chunkIndex + 1:00}" };
            generatedMeshes.Add(mesh);
            mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            var chunk = new GameObject(mesh.name);
            chunk.transform.SetParent(parent, false);
            MeshFilter filter = chunk.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            chunk.AddComponent<MeshRenderer>().sharedMaterial = profile.Material;
            LastGeneratedMeshCount++;
            LastGeneratedVertexCount += mesh.vertexCount;
            return filter;
        }

        private static void StitchChunkNormals(IReadOnlyList<MeshFilter> chunks)
        {
            for (int chunkIndex = 0; chunkIndex + 1 < chunks.Count; chunkIndex++)
            {
                Mesh previous = chunks[chunkIndex].sharedMesh;
                Mesh next = chunks[chunkIndex + 1].sharedMesh;
                Vector3[] previousNormals = previous.normals;
                Vector3[] nextNormals = next.normals;
                Vector3[] previousVertices = previous.vertices;
                Vector3[] nextVertices = next.vertices;
                int lastRow = previous.vertexCount - 4;
                for (int vertex = 0; vertex < 4; vertex++)
                {
                    if (Vector3.Distance(previousVertices[lastRow + vertex], nextVertices[vertex]) > 0.001f)
                        continue;
                    Vector3 normal = (previousNormals[lastRow + vertex] + nextNormals[vertex]).normalized;
                    previousNormals[lastRow + vertex] = normal;
                    nextNormals[vertex] = normal;
                }
                previous.normals = previousNormals;
                next.normals = nextNormals;
                previous.RecalculateTangents();
                next.RecalculateTangents();
            }
        }
    }
}
