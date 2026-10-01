using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    // Keeps heightmap samples below the finished road surface, including the
    // middle of a wide ribbon where terrain can rise above its two edge vertices.
    internal static class RoadTerrainClearanceUtility
    {
        internal static int LowerUnderRoad(Terrain terrain, SplineRoad road, float[,] heights,
            int xOffset = 0, int zOffset = 0)
        {
            if (terrain == null || road == null || road.Profile == null || heights == null)
                return 0;

            Transform root = road.transform.Find("Generated Road Mesh");
            if (root == null)
                return 0;

            TerrainData data = terrain.terrainData;
            if (data == null)
                return 0;

            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            int resolution = data.heightmapResolution;
            float clearance = Mathf.Max(0.10f, road.Profile.TerrainSurfaceOffset);
            int lowered = 0;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;

                Vector3[] vertices = mesh.vertices;
                for (int row = 0; row + 1 < vertices.Length / 4; row++)
                {
                    int current = row * 4;
                    int next = current + 4;
                    Vector3 left = filter.transform.TransformPoint(vertices[current]);
                    Vector3 right = filter.transform.TransformPoint(vertices[current + 1]);
                    Vector3 nextLeft = filter.transform.TransformPoint(vertices[next]);
                    Vector3 nextRight = filter.transform.TransformPoint(vertices[next + 1]);
                    lowered += LowerUnderTriangle(heights, origin, size, resolution, clearance,
                        left, nextLeft, right, xOffset, zOffset);
                    lowered += LowerUnderTriangle(heights, origin, size, resolution, clearance,
                        right, nextLeft, nextRight, xOffset, zOffset);
                }
            }

            return lowered;
        }

        private static int LowerUnderTriangle(
            float[,] heights, Vector3 origin, Vector3 size, int resolution, float clearance,
            Vector3 a, Vector3 b, Vector3 c, int xOffset, int zOffset)
        {
            float ax = a.x - c.x;
            float az = a.z - c.z;
            float bx = b.x - c.x;
            float bz = b.z - c.z;
            float determinant = ax * bz - bx * az;
            if (Mathf.Abs(determinant) < 0.00001f)
                return 0;

            float xScale = (resolution - 1f) / size.x;
            float zScale = (resolution - 1f) / size.z;
            int minX = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - origin.x) * xScale), 0, resolution - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - origin.x) * xScale), 0, resolution - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - origin.z) * zScale), 0, resolution - 1);
            int maxZ = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - origin.z) * zScale), 0, resolution - 1);
            minX = Mathf.Max(minX, xOffset);
            maxX = Mathf.Min(maxX, xOffset + heights.GetLength(1) - 1);
            minZ = Mathf.Max(minZ, zOffset);
            maxZ = Mathf.Min(maxZ, zOffset + heights.GetLength(0) - 1);
            int lowered = 0;

            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                float px = origin.x + x / xScale - c.x;
                float pz = origin.z + z / zScale - c.z;
                float wa = (px * bz - bx * pz) / determinant;
                float wb = (ax * pz - px * az) / determinant;
                float wc = 1f - wa - wb;
                if (wa < -0.0001f || wb < -0.0001f || wc < -0.0001f)
                    continue;

                float roadHeight = wa * a.y + wb * b.y + wc * c.y;
                float maximumTerrainHeight = Mathf.Clamp01((roadHeight - clearance - origin.y) / size.y);
                if (heights[z - zOffset, x - xOffset] <= maximumTerrainHeight)
                    continue;
                heights[z - zOffset, x - xOffset] = maximumTerrainHeight;
                lowered++;
            }

            return lowered;
        }
    }
}
