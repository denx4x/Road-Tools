using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit
{
    // Converts the conservative Bezier control polygon footprint into terrain grid rectangles.
    internal static class RoadTerrainRegion
    {
        internal static bool TryGetFootprint(Terrain terrain, IReadOnlyList<SplineRoad> roads, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (SplineRoad road in roads)
            {
                if (road == null || road.Profile == null)
                    continue;
                SplineContainer container = road.GetComponent<SplineContainer>();
                if (container == null)
                    continue;
                Bounds roadBounds = default;
                bool hasPoints = false;
                foreach (Spline spline in container.Splines)
                {
                    if (spline == null || spline.Count < 2)
                        continue;
                    foreach (BezierKnot knot in spline)
                    {
                        Include(ref roadBounds, ref hasPoints, container.transform.TransformPoint((Vector3)knot.Position));
                        Include(ref roadBounds, ref hasPoints, container.transform.TransformPoint(
                            (Vector3)(knot.Position + math.rotate(knot.Rotation, knot.TangentIn))));
                        Include(ref roadBounds, ref hasPoints, container.transform.TransformPoint(
                            (Vector3)(knot.Position + math.rotate(knot.Rotation, knot.TangentOut))));
                    }
                }
                if (!hasPoints)
                    continue;
                RoadProfile profile = road.Profile;
                PropLayerManager props = road.GetComponent<PropLayerManager>();
                float shoulder = props != null ? props.GetRoadShoulderRadius(profile) : profile.Width * 0.5f;
                float margin = shoulder + Mathf.Max(profile.TerrainBlendDistance,
                    profile.DetailClearDistance, profile.TreeClearDistance, profile.MaxTerrainPaintReach);
                TerrainData data = terrain.terrainData;
                margin += Mathf.Max(data.size.x, data.size.z) / Mathf.Max(1, data.heightmapResolution - 1) * 2f;
                roadBounds.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
                if (!found)
                    bounds = roadBounds;
                else
                    bounds.Encapsulate(roadBounds);
                found = true;
            }
            return found;
        }

        internal static RectInt GridRect(Terrain terrain, Bounds? bounds, int resolution)
        {
            if (resolution <= 0)
                return new RectInt();
            if (!bounds.HasValue)
                return new RectInt(0, 0, resolution, resolution);
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            Bounds region = bounds.Value;
            if (region.max.x < origin.x || region.min.x > origin.x + size.x ||
                region.max.z < origin.z || region.min.z > origin.z + size.z)
                return new RectInt();
            float scale = Mathf.Max(1, resolution - 1);
            int minX = Mathf.Clamp(Mathf.FloorToInt((region.min.x - origin.x) / size.x * scale), 0, resolution - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((region.min.z - origin.z) / size.z * scale), 0, resolution - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((region.max.x - origin.x) / size.x * scale), 0, resolution - 1);
            int maxZ = Mathf.Clamp(Mathf.CeilToInt((region.max.z - origin.z) / size.z * scale), 0, resolution - 1);
            return new RectInt(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
        }

        private static void Include(ref Bounds bounds, ref bool found, Vector3 position)
        {
            if (found)
                bounds.Encapsulate(position);
            else
                bounds = new Bounds(position, Vector3.zero);
            found = true;
        }
    }
}
