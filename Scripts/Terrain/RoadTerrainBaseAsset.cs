using System;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    [CreateAssetMenu(menuName = "Road Tools/Terrain Base Snapshot", fileName = "Terrain Base Snapshot")]
    public sealed class RoadTerrainBaseAsset : ScriptableObject
    {
        [SerializeField] private int heightResolution;
        [SerializeField] private int alphamapResolution;
        [SerializeField] private int alphamapLayers;
        [SerializeField] private TerrainLayer[] capturedLayers;
        [SerializeField] private int detailResolution;
        [SerializeField] private int detailLayers;
        [SerializeField] private Vector3 terrainSize;
        [SerializeField] private float[] heights;
        [SerializeField] private float[] alphamaps;
        [SerializeField] private int[] details;
        [SerializeField] private TreeInstance[] trees;

        public bool IsCaptured => heights != null && heights.Length > 0;
        internal int CaptureRevision { get; private set; }

        public bool TrySampleHeight(Terrain terrain, Vector3 worldPosition, out float worldHeight)
        {
            worldHeight = 0f;
            if (terrain == null || !Matches(terrain.terrainData))
                return false;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (size.x <= 0f || size.z <= 0f || size.y <= 0f)
                return false;
            float u = (worldPosition.x - origin.x) / size.x;
            float v = (worldPosition.z - origin.z) / size.z;
            if (u < 0f || u > 1f || v < 0f || v > 1f)
                return false;
            float x = u * (heightResolution - 1);
            float z = v * (heightResolution - 1);
            int x0 = Mathf.FloorToInt(x);
            int z0 = Mathf.FloorToInt(z);
            int x1 = Mathf.Min(x0 + 1, heightResolution - 1);
            int z1 = Mathf.Min(z0 + 1, heightResolution - 1);
            float bottom = Mathf.Lerp(heights[z0 * heightResolution + x0], heights[z0 * heightResolution + x1], x - x0);
            float top = Mathf.Lerp(heights[z1 * heightResolution + x0], heights[z1 * heightResolution + x1], x - x0);
            worldHeight = origin.y + Mathf.Lerp(bottom, top, z - z0) * size.y;
            return true;
        }

        public void CaptureFrom(TerrainData terrainData)
        {
            if (terrainData == null)
                throw new ArgumentNullException(nameof(terrainData));

            heightResolution = terrainData.heightmapResolution;
            alphamapResolution = terrainData.alphamapResolution;
            alphamapLayers = terrainData.alphamapLayers;
            capturedLayers = (TerrainLayer[])terrainData.terrainLayers.Clone();
            detailResolution = terrainData.detailResolution;
            detailLayers = terrainData.detailPrototypes.Length;
            terrainSize = terrainData.size;

            float[,] sourceHeights = terrainData.GetHeights(0, 0, heightResolution, heightResolution);
            heights = new float[heightResolution * heightResolution];
            for (int z = 0; z < heightResolution; z++)
            for (int x = 0; x < heightResolution; x++)
                heights[z * heightResolution + x] = sourceHeights[z, x];

            float[,,] sourceAlphamaps = terrainData.GetAlphamaps(
                0, 0, alphamapResolution, alphamapResolution);
            alphamaps = new float[alphamapResolution * alphamapResolution * alphamapLayers];
            for (int z = 0; z < alphamapResolution; z++)
            for (int x = 0; x < alphamapResolution; x++)
            for (int layer = 0; layer < alphamapLayers; layer++)
                alphamaps[(z * alphamapResolution + x) * alphamapLayers + layer] =
                    sourceAlphamaps[z, x, layer];

            details = new int[detailResolution * detailResolution * detailLayers];
            for (int layer = 0; layer < detailLayers; layer++)
            {
                int[,] sourceDetails = terrainData.GetDetailLayer(
                    0, 0, detailResolution, detailResolution, layer);
                for (int z = 0; z < detailResolution; z++)
                for (int x = 0; x < detailResolution; x++)
                    details[(layer * detailResolution + z) * detailResolution + x] = sourceDetails[z, x];
            }

            trees = (TreeInstance[])terrainData.treeInstances.Clone();
            CaptureRevision++;
        }

        public bool Matches(TerrainData terrainData)
        {
            if (!IsCaptured || terrainData == null)
                return false;
            TerrainLayer[] currentLayers = terrainData.terrainLayers;
            if (capturedLayers != null && capturedLayers.Length == alphamapLayers)
            {
                if (currentLayers.Length < capturedLayers.Length)
                    return false;
                for (int i = 0; i < capturedLayers.Length; i++)
                    if (currentLayers[i] != capturedLayers[i])
                        return false;
            }
            return
                   terrainData.heightmapResolution == heightResolution &&
                   terrainData.alphamapResolution == alphamapResolution &&
                   terrainData.alphamapLayers >= alphamapLayers &&
                   terrainData.detailResolution == detailResolution &&
                   terrainData.detailPrototypes.Length == detailLayers &&
                   Vector3.Distance(terrainData.size, terrainSize) < 0.001f;
        }

        public bool RestoreTo(TerrainData terrainData)
        {
            if (!Matches(terrainData))
                return false;

            Restore(terrainData, new RectInt(0, 0, heightResolution, heightResolution),
                new RectInt(0, 0, alphamapResolution, alphamapResolution),
                new RectInt(0, 0, detailResolution, detailResolution));
            return true;
        }

        internal bool RestoreRegion(Terrain terrain, Bounds worldBounds)
        {
            if (terrain == null || !Matches(terrain.terrainData))
                return false;
            Restore(terrain.terrainData,
                RoadTerrainRegion.GridRect(terrain, worldBounds, heightResolution),
                RoadTerrainRegion.GridRect(terrain, worldBounds, alphamapResolution),
                RoadTerrainRegion.GridRect(terrain, worldBounds, detailResolution));
            return true;
        }

        private void Restore(TerrainData terrainData, RectInt heightRect, RectInt alphaRect, RectInt detailRect)
        {

            if (heightRect.width > 0 && heightRect.height > 0)
            {
                var restoredHeights = new float[heightRect.height, heightRect.width];
                for (int z = 0; z < heightRect.height; z++)
                for (int x = 0; x < heightRect.width; x++)
                    restoredHeights[z, x] = heights[(z + heightRect.y) * heightResolution + x + heightRect.x];
                terrainData.SetHeightsDelayLOD(heightRect.x, heightRect.y, restoredHeights);
                terrainData.SyncHeightmap();
            }

            int currentLayerCount = terrainData.alphamapLayers;
            if (alphaRect.width > 0 && alphaRect.height > 0)
            {
                var restoredAlphamaps = new float[alphaRect.height, alphaRect.width, currentLayerCount];
                for (int z = 0; z < alphaRect.height; z++)
                for (int x = 0; x < alphaRect.width; x++)
                for (int layer = 0; layer < alphamapLayers; layer++)
                    restoredAlphamaps[z, x, layer] =
                        alphamaps[((z + alphaRect.y) * alphamapResolution + x + alphaRect.x) * alphamapLayers + layer];
                terrainData.SetAlphamaps(alphaRect.x, alphaRect.y, restoredAlphamaps);
            }

            if (detailRect.width > 0 && detailRect.height > 0)
            for (int layer = 0; layer < detailLayers; layer++)
            {
                var restoredDetails = new int[detailRect.height, detailRect.width];
                for (int z = 0; z < detailRect.height; z++)
                for (int x = 0; x < detailRect.width; x++)
                    restoredDetails[z, x] = details[(layer * detailResolution + z + detailRect.y) * detailResolution + x + detailRect.x];
                terrainData.SetDetailLayer(detailRect.x, detailRect.y, layer, restoredDetails);
            }

            terrainData.treeInstances = trees ?? Array.Empty<TreeInstance>();
        }
    }
}
