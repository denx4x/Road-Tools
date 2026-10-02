using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dyma.SplineLevelToolkit
{
    internal static class RoadFenceModelGenerator
    {
        private struct Vertex
        {
            internal Vector3 Position, Normal;
            internal Vector4 Tangent, Uv0, Uv1, Uv2, Uv3, Uv4, Uv5, Uv6, Uv7;
            internal Color Color;

            internal Vector4 Uv(int channel) => channel switch
            {
                0 => Uv0, 1 => Uv1, 2 => Uv2, 3 => Uv3,
                4 => Uv4, 5 => Uv5, 6 => Uv6, _ => Uv7
            };

            internal void SetUv(int channel, Vector4 value)
            {
                switch (channel)
                {
                    case 0: Uv0 = value; break; case 1: Uv1 = value; break;
                    case 2: Uv2 = value; break; case 3: Uv3 = value; break;
                    case 4: Uv4 = value; break; case 5: Uv5 = value; break;
                    case 6: Uv6 = value; break; default: Uv7 = value; break;
                }
            }

            internal static Vertex Lerp(Vertex a, Vertex b, float t)
            {
                var result = new Vertex
                {
                    Position = Vector3.LerpUnclamped(a.Position, b.Position, t),
                    Normal = Vector3.LerpUnclamped(a.Normal, b.Normal, t),
                    Tangent = Vector4.LerpUnclamped(a.Tangent, b.Tangent, t),
                    Color = Color.LerpUnclamped(a.Color, b.Color, t)
                };
                for (int channel = 0; channel < 8; channel++)
                    result.SetUv(channel, Vector4.LerpUnclamped(a.Uv(channel), b.Uv(channel), t));
                return result;
            }
        }

        private sealed class SourcePart
        {
            internal MeshRenderer Renderer;
            internal readonly List<Vertex>[] Triangles;
            internal readonly bool[] UvChannels = new bool[8];
            internal bool HasNormals, HasTangents, HasColors;
            internal SourcePart(int submeshes) { Triangles = new List<Vertex>[submeshes]; }
        }

        internal static int Build(Transform parent, IReadOnlyList<SplineSamplingUtility.Sample> samples,
            RoadProfile profile, SplinePropLayer layer, RoadFenceModel model, float start, float end)
        {
            if (!model.TryGetRootBounds(out Bounds bounds))
            {
                Debug.LogWarning($"Fence model '{model.name}' has no usable MeshFilter bounds.", model);
                return 0;
            }
            float length = model.Longitudinal(bounds.size);
            List<SourcePart> sources = ReadSources(model, bounds);
            if (sources == null || sources.Count == 0) return 0;

            int sides = 0;
            if (layer.Side == PropSide.Both || layer.Side == PropSide.Left)
            { BuildSide(parent, samples, profile, layer, model, bounds, length, sources, start, end, -1f); sides++; }
            if (layer.Side == PropSide.Both || layer.Side == PropSide.Right)
            { BuildSide(parent, samples, profile, layer, model, bounds, length, sources, start, end, 1f); sides++; }
            if (layer.Side == PropSide.Center)
            { BuildSide(parent, samples, profile, layer, model, bounds, length, sources, start, end, 0f); sides++; }
            return sides * Mathf.CeilToInt((end - start) / length);
        }

        private static List<SourcePart> ReadSources(RoadFenceModel model, Bounds bounds)
        {
            var sources = new List<SourcePart>();
            Matrix4x4 rootScale = Matrix4x4.Scale(model.transform.localScale);
            float minimum = model.Longitudinal(bounds.min);
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !filter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled) continue;
                if (!mesh.isReadable)
                {
                    Debug.LogWarning($"Fence mesh '{mesh.name}' requires Read/Write Enabled in its model Import Settings.", model);
                    return null;
                }
                Matrix4x4 matrix = rootScale * model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Matrix4x4 normalMatrix = matrix.inverse.transpose;
                Vector3[] positions = mesh.vertices, normals = mesh.normals;
                Vector4[] tangents = mesh.tangents;
                Color[] colors = mesh.colors;
                var source = new SourcePart(mesh.subMeshCount)
                {
                    Renderer = renderer, HasNormals = normals.Length == positions.Length,
                    HasTangents = tangents.Length == positions.Length, HasColors = colors.Length == positions.Length
                };
                var vertices = new Vertex[positions.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i].Position = matrix.MultiplyPoint3x4(positions[i]);
                    vertices[i].Normal = source.HasNormals ? normalMatrix.MultiplyVector(normals[i]).normalized : Vector3.up;
                    if (source.HasTangents)
                    {
                        Vector3 tangent = matrix.MultiplyVector((Vector3)tangents[i]).normalized;
                        vertices[i].Tangent = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
                    }
                    vertices[i].Color = source.HasColors ? colors[i] : Color.white;
                }
                for (int channel = 0; channel < 8; channel++)
                {
                    var uv = new List<Vector4>(); mesh.GetUVs(channel, uv);
                    source.UvChannels[channel] = uv.Count == vertices.Length;
                    if (source.UvChannels[channel])
                        for (int i = 0; i < vertices.Length; i++) vertices[i].SetUv(channel, uv[i]);
                }
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    source.Triangles[submesh] = new List<Vertex>();
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                    int[] indices = mesh.GetTriangles(submesh);
                    for (int triangle = 0; triangle < indices.Length; triangle += 3)
                        SubdivideTriangle(source.Triangles[submesh], vertices[indices[triangle]],
                            vertices[indices[triangle + 1]], vertices[indices[triangle + 2]], model, minimum);
                }
                sources.Add(source);
            }
            return sources;
        }

        private static void SubdivideTriangle(List<Vertex> output, Vertex a, Vertex b, Vertex c,
            RoadFenceModel model, float minimum)
        {
            float low = Mathf.Min(model.Longitudinal(a.Position), model.Longitudinal(b.Position), model.Longitudinal(c.Position));
            float high = Mathf.Max(model.Longitudinal(a.Position), model.Longitudinal(b.Position), model.Longitudinal(c.Position));
            float step = model.MeshSubdivisionLength;
            int first = Mathf.FloorToInt((low - minimum) / step);
            int last = Mathf.FloorToInt((high - minimum) / step);
            if (last <= first) { output.Add(a); output.Add(b); output.Add(c); return; }
            for (int slice = first; slice <= last; slice++)
            {
                var polygon = new List<Vertex>(6) { a, b, c };
                polygon = Clip(polygon, model, minimum + slice * step, true);
                polygon = Clip(polygon, model, minimum + (slice + 1) * step, false);
                AddTriangles(output, polygon);
            }
        }

        private static List<Vertex> Clip(List<Vertex> polygon, RoadFenceModel model, float plane, bool keepAbove)
        {
            var result = new List<Vertex>(6);
            if (polygon.Count == 0) return result;
            Vertex previous = polygon[polygon.Count - 1];
            float previousDistance = model.Longitudinal(previous.Position) - plane;
            bool previousInside = keepAbove ? previousDistance >= -0.00001f : previousDistance <= 0.00001f;
            foreach (Vertex current in polygon)
            {
                float distance = model.Longitudinal(current.Position) - plane;
                bool inside = keepAbove ? distance >= -0.00001f : distance <= 0.00001f;
                if (inside != previousInside)
                {
                    float span = previousDistance - distance;
                    if (Mathf.Abs(span) > 0.000001f)
                        result.Add(Vertex.Lerp(previous, current, previousDistance / span));
                }
                if (inside) result.Add(current);
                previous = current; previousDistance = distance; previousInside = inside;
            }
            return result;
        }

        private static void AddTriangles(List<Vertex> output, List<Vertex> polygon)
        {
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                if (Vector3.Cross(polygon[i].Position - polygon[0].Position,
                    polygon[i + 1].Position - polygon[0].Position).sqrMagnitude < 0.0000000001f) continue;
                output.Add(polygon[0]); output.Add(polygon[i]); output.Add(polygon[i + 1]);
            }
        }

        private static void BuildSide(Transform parent, IReadOnlyList<SplineSamplingUtility.Sample> samples,
            RoadProfile profile, SplinePropLayer layer, RoadFenceModel model, Bounds bounds, float segmentLength,
            List<SourcePart> sources, float start, float end, float side)
        {
            float minimum = model.Longitudinal(bounds.min);
            bool mirror = side > 0f && model.MirrorRightSide;
            float lateral = side == 0f ? layer.LateralOffset : side * (profile.Width * 0.5f + layer.LateralOffset);
            RoadFencePath path = RoadFencePath.Build(samples, lateral, start, end,
                profile.ConformRoadToTerrain || layer.Grounding == PropGroundingMode.Terrain,
                layer.VerticalOffset, model.HeightSmoothingDistance);
            var deformation = new RoadFenceDeformation(path, model.GroundedSupportHeight);
            foreach (SourcePart source in sources)
            {
                var vertices = new List<Vector3>(); var normals = new List<Vector3>();
                var tangents = new List<Vector4>(); var colors = new List<Color>();
                var uvs = new List<Vector4>[8];
                for (int channel = 0; channel < 8; channel++)
                    if (source.UvChannels[channel]) uvs[channel] = new List<Vector4>();
                var triangles = new List<int>[source.Triangles.Length];
                for (int submesh = 0; submesh < triangles.Length; submesh++) triangles[submesh] = new List<int>();

                for (float segmentStart = start; segmentStart < end - 0.0001f; segmentStart += segmentLength)
                {
                    float remaining = Mathf.Min(segmentLength, end - segmentStart);
                    for (int submesh = 0; submesh < triangles.Length; submesh++)
                    {
                        List<Vertex> input = source.Triangles[submesh];
                        for (int triangle = 0; triangle < input.Count; triangle += 3)
                        {
                            List<Vertex> clipped = input;
                            int firstVertex = triangle;
                            int lastVertex = triangle + 3;
                            if (remaining < segmentLength - 0.0001f)
                            {
                                var polygon = new List<Vertex>(4) { input[triangle], input[triangle + 1], input[triangle + 2] };
                                polygon = Clip(polygon, model, minimum + remaining, false);
                                clipped = new List<Vertex>(); AddTriangles(clipped, polygon);
                                firstVertex = 0;
                                lastVertex = clipped.Count;
                            }
                            for (int i = firstVertex; i < lastVertex; i++)
                            {
                                Vertex vertex = clipped[i];
                                float distance = segmentStart + model.Longitudinal(vertex.Position) - minimum;
                                float across = model.Across(vertex.Position) * (mirror ? -1f : 1f);
                                Vector3 position = deformation.Transform(distance, across,
                                    vertex.Position.y - bounds.min.y, out Matrix4x4 basis);
                                int index = vertices.Count;
                                vertices.Add(parent.InverseTransformPoint(position));
                                Vector3 normal = basis.inverse.transpose.MultiplyVector(
                                    ModelDirection(vertex.Normal, model, mirror)).normalized;
                                normals.Add(parent.localToWorldMatrix.transpose.MultiplyVector(normal).normalized);
                                if (source.HasTangents)
                                {
                                    Vector3 tangent = parent.InverseTransformVector(
                                        basis.MultiplyVector(ModelDirection((Vector3)vertex.Tangent, model, mirror))).normalized;
                                    tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, vertex.Tangent.w * (mirror ? -1f : 1f)));
                                }
                                if (source.HasColors) colors.Add(vertex.Color);
                                for (int channel = 0; channel < 8; channel++)
                                    if (uvs[channel] != null) uvs[channel].Add(vertex.Uv(channel));
                                // Reflection reverses winding so the visible rail faces the road on both sides.
                                int corner = (i - firstVertex) % 3;
                                triangles[submesh].Add(mirror && corner > 0 ? index + (corner == 1 ? 1 : -1) : index);
                            }
                        }
                    }
                }
                if (vertices.Count == 0) continue;
                var mesh = new Mesh { name = "Road Fence Model " + source.Renderer.name };
                if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.subMeshCount = triangles.Length;
                for (int submesh = 0; submesh < triangles.Length; submesh++) mesh.SetTriangles(triangles[submesh], submesh, false);
                if (source.HasNormals) mesh.SetNormals(normals); else mesh.RecalculateNormals();
                for (int channel = 0; channel < 8; channel++) if (uvs[channel] != null) mesh.SetUVs(channel, uvs[channel]);
                if (source.HasTangents) mesh.SetTangents(tangents); else if (source.UvChannels[0]) mesh.RecalculateTangents();
                if (source.HasColors) mesh.SetColors(colors);
                mesh.RecalculateBounds();
                var part = new GameObject($"Road Fence Model {(side < 0f ? "Left" : side > 0f ? "Right" : "Center")} - {source.Renderer.name}");
                part.transform.SetParent(parent, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.Renderer.sharedMaterials;
                renderer.shadowCastingMode = source.Renderer.shadowCastingMode;
                renderer.receiveShadows = source.Renderer.receiveShadows;
                part.AddComponent<RoadFenceGeneratedMesh>().Initialize(mesh);
            }
        }

        private static Vector3 ModelDirection(Vector3 value, RoadFenceModel model, bool mirror)
        {
            return new Vector3(model.Across(value) * (mirror ? -1f : 1f), value.y, model.Longitudinal(value));
        }
    }
}
