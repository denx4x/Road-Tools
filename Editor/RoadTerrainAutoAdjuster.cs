using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadTerrainAutoAdjuster
    {
        internal static bool TryAdjust(SplineRoad road, Terrain terrain, out string message)
        {
            if (road == null || road.Profile == null || road.IsBaked || terrain == null || terrain.terrainData == null)
            {
                message = "Select an editable road with a profile and a valid Terrain.";
                return false;
            }

            SplineContainer container = road.GetComponent<SplineContainer>();
            if (container == null || container.Splines.Count == 0)
            {
                message = "The selected road has no spline.";
                return false;
            }

            RoadTerrainManager manager = terrain.GetComponent<RoadTerrainManager>();
            RoadTerrainBaseAsset snapshot = manager != null ? manager.BaseSnapshot : null;
            if (snapshot != null && !snapshot.Matches(terrain.terrainData))
            {
                message = "The terrain base snapshot does not match this terrain. Assign a matching snapshot first.";
                return false;
            }

            int knotCount = 0;
            Vector3 terrainOrigin = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            foreach (Spline spline in container.Splines)
            {
                if (spline == null) continue;
                for (int i = 0; i < spline.Count; i++)
                {
                    Vector3 worldPosition = container.transform.TransformPoint((Vector3)spline[i].Position);
                    bool inside = worldPosition.x >= terrainOrigin.x && worldPosition.x <= terrainOrigin.x + terrainSize.x &&
                                  worldPosition.z >= terrainOrigin.z && worldPosition.z <= terrainOrigin.z + terrainSize.z;
                    if (!inside)
                    {
                        message = "Every road point must be inside the selected terrain. Move the road or choose another terrain.";
                        return false;
                    }
                    knotCount++;
                }
            }
            if (knotCount < 2)
            {
                message = "The road needs at least two spline points.";
                return false;
            }

            if (snapshot == null)
            {
                if (manager == null)
                    manager = Undo.AddComponent<RoadTerrainManager>(terrain.gameObject);
                RoadTerrainManagerEditor.Capture(manager, terrain);
                snapshot = manager.BaseSnapshot;
            }

            Undo.RecordObject(container, "Adjust Road Height to Terrain");
            Undo.RecordObject(road, "Enable Road Terrain Adjustment");
            Undo.RecordObject(manager, "Register Road Terrain Adjustment");
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Adjust Terrain Around Road");

            float clearance = road.Profile.TerrainSurfaceOffset;
            foreach (Spline spline in container.Splines)
            {
                if (spline == null) continue;
                for (int i = 0; i < spline.Count; i++)
                {
                    BezierKnot knot = spline[i];
                    Vector3 worldPosition = container.transform.TransformPoint((Vector3)knot.Position);
                    snapshot.TrySampleHeight(terrain, worldPosition, out float baseHeight);
                    worldPosition.y = baseHeight + clearance;
                    knot.Position = (float3)container.transform.InverseTransformPoint(worldPosition);
                    spline.SetKnot(i, knot);
                }
            }

            road.SetTerrainAdjustment(true);
            manager.AddRoad(road);
            EditorUtility.SetDirty(container);
            EditorUtility.SetDirty(road);
            EditorUtility.SetDirty(manager);
            string terrainResult = manager.RebuildTerrain();
            if (!manager.LastRebuildSucceeded)
            {
                message = terrainResult;
                return false;
            }

            RoadBuildReport build = road.LastBuildReport;
            EditorUtility.SetDirty(terrain.terrainData);
            EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            message = $"Adjusted {knotCount} spline points to the terrain. {terrainResult} {build.Message}";
            return build.Succeeded;
        }
    }
}
