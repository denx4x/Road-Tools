using System.Collections.Generic;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    internal static class RoadTerrainBandPainter
    {
        private readonly struct ResolvedBand
        {
            internal ResolvedBand(RoadTerrainPaintBand band, int layerIndex)
            {
                Band = band;
                LayerIndex = layerIndex;
            }

            internal RoadTerrainPaintBand Band { get; }
            internal int LayerIndex { get; }
        }

        internal static bool Paint(Terrain terrain, RoadTerrainPath path, RoadProfile profile, Bounds? region = null)
        {
            TerrainData data = terrain.terrainData;
            TerrainLayer[] terrainLayers = data.terrainLayers;
            var bands = new List<ResolvedBand>();
            foreach (RoadTerrainPaintBand band in profile.TerrainPaintBands)
            {
                if (band == null || band.Layer == null || band.Width <= 0f)
                    continue;
                int index = System.Array.IndexOf(terrainLayers, band.Layer);
                if (index >= 0)
                    bands.Add(new ResolvedBand(band, index));
            }
            if (bands.Count == 0)
                return false;

            int resolution = data.alphamapResolution;
            int layerCount = data.alphamapLayers;
            RectInt rectangle = RoadTerrainRegion.GridRect(terrain, region, resolution);
            if (rectangle.width <= 0 || rectangle.height <= 0)
                return true;
            float[,,] alphamaps = data.GetAlphamaps(rectangle.x, rectangle.y, rectangle.width, rectangle.height);
            float[] weights = new float[bands.Count];
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            float halfWidth = profile.Width * 0.5f;

            for (int z = 0; z < rectangle.height; z++)
            for (int x = 0; x < rectangle.width; x++)
            {
                Vector3 position = new(origin.x + (x + rectangle.x) / Mathf.Max(1f, resolution - 1f) * size.x,
                    0f, origin.z + (z + rectangle.y) / Mathf.Max(1f, resolution - 1f) * size.z);
                if (!path.TryNearest(position, out float distance, out _))
                    continue;

                float outward = Mathf.Max(0f, distance - halfWidth);
                float sum = 0f;
                for (int i = 0; i < bands.Count; i++)
                {
                    weights[i] = Weight(bands[i].Band, outward);
                    sum += weights[i];
                }
                if (sum <= 0f)
                    continue;

                float coverage = Mathf.Min(1f, sum);
                for (int layer = 0; layer < layerCount; layer++)
                    alphamaps[z, x, layer] *= 1f - coverage;
                float normalization = coverage / sum;
                for (int i = 0; i < bands.Count; i++)
                    alphamaps[z, x, bands[i].LayerIndex] += weights[i] * normalization;
            }

            data.SetAlphamaps(rectangle.x, rectangle.y, alphamaps);
            return true;
        }

        private static float Weight(RoadTerrainPaintBand band, float distance)
        {
            float blend = Mathf.Max(0f, band.BlendDistance);
            float start = Mathf.Max(0f, band.StartDistance);
            float end = start + Mathf.Max(0.1f, band.Width);
            if (blend <= 0.001f)
                return distance >= start && distance <= end ? 1f : 0f;

            float entry = start <= 0f ? 1f : SmoothStep(start - blend * 0.5f, start + blend * 0.5f, distance);
            float exit = 1f - SmoothStep(end - blend * 0.5f, end + blend * 0.5f, distance);
            return entry * exit;
        }

        private static float SmoothStep(float start, float end, float value)
        {
            float t = Mathf.Clamp01((value - start) / Mathf.Max(0.001f, end - start));
            return t * t * (3f - 2f * t);
        }
    }
}
