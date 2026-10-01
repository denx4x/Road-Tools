using System.Collections.Generic;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain))]
    public sealed class RoadTerrainManager : MonoBehaviour
    {
        [SerializeField] private RoadTerrainBaseAsset baseSnapshot;
        [SerializeField] private List<SplineRoad> roads = new();
        private bool liveCacheInitialized;
        private bool hasPreviousFootprint;
        private Bounds previousFootprint;
        private TerrainData cachedTerrainData;
        private RoadTerrainBaseAsset cachedSnapshot;
        private int cachedCaptureRevision;
        private Vector3 cachedTerrainPosition;

        public RoadTerrainBaseAsset BaseSnapshot => baseSnapshot;
        public IReadOnlyList<SplineRoad> Roads => roads;
        public bool LastRebuildSucceeded { get; private set; }
        public int LastRoadRefreshCount { get; private set; }
        public int LastClearedTerrainSamples { get; private set; }
        public int LastRefreshedPropCount { get; private set; }
        public bool LastUsedRegionalRestore { get; private set; }
        public int LastProcessedHeightSamples { get; private set; }

        public void SetBaseSnapshot(RoadTerrainBaseAsset value)
        {
            baseSnapshot = value;
            InvalidateLiveEditCache();
        }

        public void InvalidateLiveEditCache() => liveCacheInitialized = false;

        public bool AddRoad(SplineRoad road)
        {
            if (road == null || roads.Contains(road))
                return false;
            roads.Add(road);
            return true;
        }

        public bool RemoveRoad(SplineRoad road) => roads.Remove(road);

        public string RebuildTerrain()
        {
            return RebuildTerrainCore(false);
        }

        // The first update establishes a full baseline. Later updates restore both the old
        // and new road footprints, so moving or undoing a spline leaves no terrain trails.
        public string RebuildTerrainForLiveEdit()
        {
            return RebuildTerrainCore(true);
        }

        private string RebuildTerrainCore(bool liveEdit)
        {
            LastRebuildSucceeded = false;
            LastRoadRefreshCount = 0;
            LastClearedTerrainSamples = 0;
            LastRefreshedPropCount = 0;
            LastUsedRegionalRestore = false;
            LastProcessedHeightSamples = 0;
            Terrain terrain = GetComponent<Terrain>();
            if (terrain == null || terrain.terrainData == null)
                return "Terrain data is missing.";
            if (baseSnapshot == null)
                return "Capture a terrain base snapshot first.";
            if (!baseSnapshot.Matches(terrain.terrainData))
                return "Terrain base snapshot does not match the original terrain layers or resolution.";
            int addedLayers = RoadTerrainLayerUtility.EnsurePaintLayers(terrain.terrainData, roads);
            if (!baseSnapshot.Matches(terrain.terrainData))
                return "Capture a matching terrain base snapshot first.";

            bool hasCurrentFootprint = RoadTerrainRegion.TryGetFootprint(terrain, roads, out Bounds currentFootprint);
            bool canRestoreRegion = liveEdit && liveCacheInitialized && addedLayers == 0 &&
                cachedTerrainData == terrain.terrainData && cachedSnapshot == baseSnapshot &&
                cachedCaptureRevision == baseSnapshot.CaptureRevision &&
                cachedTerrainPosition == terrain.transform.position && (hasPreviousFootprint || hasCurrentFootprint);
            Bounds? region = null;
            if (canRestoreRegion)
            {
                Bounds affected = hasPreviousFootprint ? previousFootprint : currentFootprint;
                if (hasCurrentFootprint)
                    affected.Encapsulate(currentFootprint);
                region = affected;
            }
            if (!(region.HasValue ? baseSnapshot.RestoreRegion(terrain, region.Value) : baseSnapshot.RestoreTo(terrain.terrainData)))
                return "Could not restore the terrain base snapshot.";
            LastUsedRegionalRestore = region.HasValue;
            RectInt heightRectangle = RoadTerrainRegion.GridRect(terrain, region, terrain.terrainData.heightmapResolution);
            LastProcessedHeightSamples = heightRectangle.width * heightRectangle.height;

            int applied = 0;
            foreach (SplineRoad road in roads)
            {
                if (road == null || road.Profile == null ||
                    (!road.Profile.UsesTerrain && !road.AdjustTerrainToRoad))
                    continue;
                RoadTerrainModifier.Apply(terrain, road, region);
                applied++;
            }

            terrain.Flush();
            int failed = 0;
            int baked = 0;
            foreach (SplineRoad road in roads)
            {
                if (road == null || road.Profile == null)
                    continue;
                if (road.IsBaked)
                {
                    baked++;
                    continue;
                }

                RoadBuildReport result = road.Rebuild(false);
                if (result.Succeeded)
                    LastRoadRefreshCount++;
                else
                    failed++;
            }

            float[,] clearanceHeights = null;
            foreach (SplineRoad road in roads)
            {
                if (road == null || road.Profile == null ||
                    (!road.Profile.DeformTerrain && !road.AdjustTerrainToRoad) ||
                    heightRectangle.width <= 0 || heightRectangle.height <= 0)
                    continue;
                clearanceHeights ??= terrain.terrainData.GetHeights(
                    heightRectangle.x, heightRectangle.y, heightRectangle.width, heightRectangle.height);
                LastClearedTerrainSamples += RoadTerrainClearanceUtility.LowerUnderRoad(
                    terrain, road, clearanceHeights, heightRectangle.x, heightRectangle.y);
            }
            if (LastClearedTerrainSamples > 0)
            {
                terrain.terrainData.SetHeightsDelayLOD(heightRectangle.x, heightRectangle.y, clearanceHeights);
                terrain.terrainData.SyncHeightmap();
                terrain.Flush();
            }

            foreach (SplineRoad road in roads)
            {
                if (road == null || road.Profile == null || road.IsBaked ||
                    !road.LastBuildReport.Succeeded)
                    continue;
                road.RebuildProps();
                PropLayerManager props = road.GetComponent<PropLayerManager>();
                if (props != null)
                    LastRefreshedPropCount += props.LastGeneratedInstanceCount;
            }

            LastRebuildSucceeded = failed == 0;
            liveCacheInitialized = true;
            hasPreviousFootprint = hasCurrentFootprint;
            previousFootprint = currentFootprint;
            cachedTerrainData = terrain.terrainData;
            cachedSnapshot = baseSnapshot;
            cachedCaptureRevision = baseSnapshot.CaptureRevision;
            cachedTerrainPosition = terrain.transform.position;
            return $"Restored terrain base and applied {applied} road modifiers. Added {addedLayers} terrain layers. " +
                   $"Refreshed {LastRoadRefreshCount} roads" +
                   (failed > 0 ? $"; {failed} road rebuilds failed" : "") +
                   (baked > 0 ? $"; {baked} baked roads skipped" : "") +
                   $". Lowered {LastClearedTerrainSamples} terrain samples beneath road meshes. " +
                   $"Placed {LastRefreshedPropCount} props on the final terrain.";
        }
    }
}
