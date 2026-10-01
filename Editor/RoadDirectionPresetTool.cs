using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadDirectionPresetTool
    {
        internal static bool TryGetSelectedKnot(SplineRoad road, out int splineIndex, out int knotIndex)
        {
            splineIndex = -1;
            knotIndex = -1;
            if (road == null || SplineSelection.Count == 0)
                return false;

            SplineContainer container = road.GetComponent<SplineContainer>();
            if (container == null)
                return false;

            var splineInfos = new List<SplineInfo>(container.Splines.Count);
            for (int i = 0; i < container.Splines.Count; i++)
                splineInfos.Add(new SplineInfo(container, i));

            var selected = new List<SelectableKnot>();
            SplineSelection.GetElements(splineInfos, selected);
            if (selected.Count != 1 || (Object)selected[0].SplineInfo.Container != container)
                return false;

            Spline spline = selected[0].SplineInfo.Spline;
            for (int i = 0; i < container.Splines.Count; i++)
            {
                if (container.Splines[i] != spline)
                    continue;
                if (selected[0].KnotIndex < 0 || selected[0].KnotIndex >= spline.Count)
                    return false;
                splineIndex = i;
                knotIndex = selected[0].KnotIndex;
                return true;
            }
            return false;
        }

        internal static bool Apply(SplineRoad road, float length, float angleDegrees, out string message)
            => Apply(road, length, angleDegrees, road != null && road.Profile != null ? road.Profile.Width * 2f : 12f, out message);

        internal static bool Apply(SplineRoad road, float length, float angleDegrees, float radius, out string message)
        {
            if (road == null || road.IsBaked || road.Profile == null ||
                !TryGetSelectedKnot(road, out int splineIndex, out int knotIndex))
            {
                message = "Select exactly one knot on an editable road with a Road Profile.";
                return false;
            }
            if (length < 0.5f || float.IsNaN(length) || float.IsInfinity(length) ||
                float.IsNaN(angleDegrees) || float.IsInfinity(angleDegrees))
            {
                message = "Use a segment length of at least 0.5 m and a finite angle.";
                return false;
            }

            SplineContainer container = road.GetComponent<SplineContainer>();
            Spline spline = container.Splines[splineIndex];
            if (spline.Closed || spline.Count < 2)
            {
                message = "Direction presets require an open spline with at least two points.";
                return false;
            }

            RoadTurnPlan plan = RoadTurnPlan.Create(road, length, angleDegrees, radius);
            if (!plan.Safe) { message = plan.Message; return false; }
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Road Turn");
            Undo.RecordObject(container, "Apply Road Turn");
            var replacement = new List<Spline>(container.Splines);
            replacement[splineIndex] = plan.Spline;
            container.Splines = replacement;
            int affectedIndex = plan.EndIndex;
            EditorUtility.SetDirty(container);

            bool terrainRebuilt = RebuildRegisteredTerrain(road, out string terrainStatus, out bool terrainFailed);
            RoadBuildReport build = terrainRebuilt && road.LastBuildReport.Succeeded
                ? road.LastBuildReport
                : road.Rebuild();
            SplineSelection.Set(new SelectableKnot(new SplineInfo(container, splineIndex), affectedIndex));
            EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
            SceneView.RepaintAll();
            Undo.CollapseUndoOperations(undoGroup);
            message = $"Applied {angleDegrees:+0.#;-0.#;0}° turn, radius {radius:0.#} m, straight length {length:0.#} m. " +
                      build.Message + terrainStatus;
            return build.Succeeded && !terrainFailed;
        }

        private static bool RebuildRegisteredTerrain(SplineRoad road, out string status, out bool failed)
        {
            status = "";
            failed = false;
            bool rebuilt = false;
            foreach (RoadTerrainManager manager in Object.FindObjectsByType<RoadTerrainManager>(FindObjectsSortMode.None))
            {
                if (manager == null || manager.BaseSnapshot == null)
                    continue;
                bool registered = false;
                foreach (SplineRoad listed in manager.Roads)
                    registered |= listed == road;
                if (!registered)
                    continue;

                Terrain terrain = manager.GetComponent<Terrain>();
                if (terrain == null || terrain.terrainData == null)
                    continue;
                Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Set Road Direction");
                string result = manager.RebuildTerrain();
                EditorUtility.SetDirty(terrain.terrainData);
                EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
                rebuilt |= manager.LastRebuildSucceeded;
                failed |= !manager.LastRebuildSucceeded;
                status += manager.LastRebuildSucceeded ? " Terrain updated." : " Terrain: " + result;
            }
            return rebuilt;
        }
    }
}
