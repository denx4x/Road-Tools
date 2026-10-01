using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    public static class RoadTerrainHeightUtility
    {
        public static Vector3 Conform(Vector3 worldPosition, float offset)
        {
            if (TrySampleHeight(worldPosition, out float height))
                worldPosition.y = height + offset;
            return worldPosition;
        }

        internal static bool TrySampleHeight(Vector3 worldPosition, out float height)
        {
            height = 0f;
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                if (terrain == null || terrain.terrainData == null)
                    continue;

                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (worldPosition.x < origin.x || worldPosition.x > origin.x + size.x ||
                    worldPosition.z < origin.z || worldPosition.z > origin.z + size.z)
                    continue;

                height = terrain.SampleHeight(worldPosition) + origin.y;
                return true;
            }

            return false;
        }
    }
}
