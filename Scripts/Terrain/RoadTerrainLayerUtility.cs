using System.Collections.Generic;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    internal static class RoadTerrainLayerUtility
    {
        internal static int EnsurePaintLayers(TerrainData data, IReadOnlyList<SplineRoad> roads)
        {
            var layers = new List<TerrainLayer>(data.terrainLayers);
            int added = 0;
            foreach (SplineRoad road in roads)
            {
                if (road == null || road.Profile == null)
                    continue;
                foreach (RoadTerrainPaintBand band in road.Profile.TerrainPaintBands)
                {
                    if (band == null || band.Layer == null || layers.Contains(band.Layer))
                        continue;
                    layers.Add(band.Layer);
                    added++;
                }
            }
            if (added > 0)
                data.terrainLayers = layers.ToArray();
            return added;
        }
    }
}
