using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit
{
    public static class RoadTerrainModifier
    {
        public static void Apply(Terrain terrain, SplineRoad road)
        {
            Apply(terrain, road, null);
        }

        internal static void Apply(Terrain terrain, SplineRoad road, Bounds? region)
        {
            if (terrain == null || terrain.terrainData == null || road == null || road.Profile == null)
                return;
            SplineContainer container = road.GetComponent<SplineContainer>();
            if (container == null)
                return;

            RoadProfile profile = road.Profile;
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            float halfWidth = profile.Width * 0.5f;
            PropLayerManager propManager = road.GetComponent<PropLayerManager>();
            float shoulderRadius = propManager != null
                ? propManager.GetRoadShoulderRadius(profile)
                : halfWidth;
            float searchRadius = shoulderRadius + Mathf.Max(profile.TerrainBlendDistance,
                profile.DetailClearDistance, profile.TreeClearDistance, profile.MaxTerrainPaintReach);
            var path = new RoadTerrainPath(container, Mathf.Min(1f, profile.SampleSpacing), searchRadius);

            if (profile.DeformTerrain || road.AdjustTerrainToRoad)
            {
                int resolution = data.heightmapResolution;
                RectInt rectangle = RoadTerrainRegion.GridRect(terrain, region, resolution);
                if (rectangle.width > 0 && rectangle.height > 0)
                {
                    float[,] heights = data.GetHeights(rectangle.x, rectangle.y, rectangle.width, rectangle.height);
                    for (int z = 0; z < rectangle.height; z++)
                    for (int x = 0; x < rectangle.width; x++)
                    {
                        Vector3 position = GridPoint(origin, size, x + rectangle.x, z + rectangle.y, resolution);
                        if (!path.TryNearest(position, out float distance, out float roadHeight))
                            continue;
                        float weight = Falloff(distance, shoulderRadius, profile.TerrainBlendDistance);
                        if (weight <= 0f)
                            continue;
                        float target = Mathf.Clamp01((roadHeight - origin.y - profile.TerrainSurfaceOffset) / size.y);
                        heights[z, x] = Mathf.Lerp(heights[z, x], target, weight);
                    }
                    data.SetHeightsDelayLOD(rectangle.x, rectangle.y, heights);
                    data.SyncHeightmap();
                }
            }

            if (profile.ClearTerrainDetails)
            {
                int resolution = data.detailResolution;
                RectInt rectangle = RoadTerrainRegion.GridRect(terrain, region, resolution);
                if (rectangle.width > 0 && rectangle.height > 0)
                for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
                {
                    int[,] details = data.GetDetailLayer(rectangle.x, rectangle.y, rectangle.width, rectangle.height, layer);
                    for (int z = 0; z < rectangle.height; z++)
                    for (int x = 0; x < rectangle.width; x++)
                    {
                        Vector3 position = GridPoint(origin, size, x + rectangle.x, z + rectangle.y, resolution);
                        if (path.TryNearest(position, out float distance, out _) &&
                            distance <= halfWidth + profile.DetailClearDistance)
                            details[z, x] = 0;
                    }
                    data.SetDetailLayer(rectangle.x, rectangle.y, layer, details);
                }
            }

            if (profile.ClearTerrainTrees)
            {
                var kept = new List<TreeInstance>();
                foreach (TreeInstance tree in data.treeInstances)
                {
                    Vector3 position = new(origin.x + tree.position.x * size.x, 0f,
                        origin.z + tree.position.z * size.z);
                    if (!path.TryNearest(position, out float distance, out _) ||
                        distance > halfWidth + profile.TreeClearDistance)
                        kept.Add(tree);
                }
                data.treeInstances = kept.ToArray();
            }

            if (!RoadTerrainBandPainter.Paint(terrain, path, profile, region) &&
                profile.PaintTerrainLayer && profile.TerrainLayerIndex < data.alphamapLayers)
            {
                int resolution = data.alphamapResolution;
                int layerCount = data.alphamapLayers;
                RectInt rectangle = RoadTerrainRegion.GridRect(terrain, region, resolution);
                if (rectangle.width <= 0 || rectangle.height <= 0)
                    return;
                float[,,] alphamaps = data.GetAlphamaps(rectangle.x, rectangle.y, rectangle.width, rectangle.height);
                for (int z = 0; z < rectangle.height; z++)
                for (int x = 0; x < rectangle.width; x++)
                {
                    Vector3 position = GridPoint(origin, size, x + rectangle.x, z + rectangle.y, resolution);
                    if (!path.TryNearest(position, out float distance, out _))
                        continue;
                    float weight = Falloff(distance, halfWidth, profile.TerrainPaintBlendDistance);
                    if (weight <= 0f)
                        continue;
                    for (int layer = 0; layer < layerCount; layer++)
                        alphamaps[z, x, layer] *= 1f - weight;
                    alphamaps[z, x, profile.TerrainLayerIndex] += weight;
                }
                data.SetAlphamaps(rectangle.x, rectangle.y, alphamaps);
            }
        }

        private static Vector3 GridPoint(Vector3 origin, Vector3 size, int x, int z, int resolution)
        {
            float denominator = Mathf.Max(1f, resolution - 1f);
            return new Vector3(origin.x + x / denominator * size.x, 0f,
                origin.z + z / denominator * size.z);
        }

        private static float Falloff(float distance, float halfWidth, float blend)
        {
            if (distance <= halfWidth)
                return 1f;
            if (blend <= 0f)
                return 0f;
            float t = Mathf.Clamp01((distance - halfWidth) / blend);
            return 1f - t * t * (3f - 2f * t);
        }
    }
}
