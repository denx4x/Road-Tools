using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadKnotConnectionUtility
    {
        internal static bool Merge(RoadKnotConnectionReference a, RoadKnotConnectionReference b,
            out SplineRoad mergedRoad, out string message)
        {
            mergedRoad = null;
            RoadKnotConnectionPlan plan = RoadKnotConnectionGeometry.Plan(a, b);
            message = plan.Message;
            if (!plan.Succeeded) return false;
            if (plan.TightBend)
            {
                message = "The bridge would be too tight for this road width. Move the endpoints farther apart or rotate their handles before merging.";
                return false;
            }

            SplineContainer containerA = a.Container, containerB = b.Container;
            SplineRoad roadA = containerA.GetComponent<SplineRoad>(), roadB = containerB.GetComponent<SplineRoad>();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Merge Road Splines");
            // Unity Splines' own authoring utilities use property-diff recording for managed spline collections.
            Undo.RecordObject(containerA, "Merge Road Splines");
            if (containerA != containerB) Undo.RecordObject(containerB, "Merge Road Splines");
            Undo.RegisterCompleteObjectUndo(roadA, "Merge Road Splines");
            if (roadA != roadB) Undo.RegisterCompleteObjectUndo(roadB, "Merge Road Splines");

            try
            {
                var splinesA = new List<Spline>(containerA.Splines);
                splinesA[a.SplineIndex] = plan.MergedSpline;
                // Build the merged road before consuming B. Failed builds restore the whole authoring transaction.
                containerA.Splines = splinesA;
                RoadBuildReport build = roadA.Rebuild();
                if (!build.Succeeded) throw new InvalidOperationException(build.Message);

                if (containerA == containerB)
                {
                    containerA.RemoveSplineAt(b.SplineIndex);
                    build = roadA.Rebuild();
                    if (!build.Succeeded) throw new InvalidOperationException(build.Message);
                }
                else
                {
                    containerB.RemoveSplineAt(b.SplineIndex);
                    if (containerB.Splines.Count > 0) roadB.Rebuild();
                    else
                    {
                        // Keep the user's GameObject, unrelated components and children intact.
                        // Generated meshes/props are derived data and return through live rebuilding on Undo.
                        containerB.GetComponent<RoadMeshGenerator>()?.ClearGenerated();
                        containerB.GetComponent<PropLayerManager>()?.ClearGenerated();
                        roadB.enabled = false;
                        foreach (RoadTerrainManager manager in UnityEngine.Object.FindObjectsByType<RoadTerrainManager>(FindObjectsSortMode.None))
                        {
                            bool registered = false;
                            foreach (SplineRoad road in manager.Roads) registered |= road == roadB;
                            if (!registered) continue;
                            Undo.RecordObject(manager, "Merge Road Splines");
                            manager.RemoveRoad(roadB);
                            manager.InvalidateLiveEditCache();
                            EditorUtility.SetDirty(manager);
                        }
                    }
                    EditorUtility.SetDirty(containerB);
                    EditorUtility.SetDirty(roadB);
                }

                roadA.SetLiveUpdates(true);
                RoadLiveUpdateCoordinator.EnsureTerrainRegistration(roadA);
                roadA.RequestRebuild();
                if (roadB != roadA && roadB.isActiveAndEnabled) roadB.RequestRebuild();
                EditorUtility.SetDirty(containerA);
                EditorUtility.SetDirty(roadA);
                EditorSceneManager.MarkSceneDirty(containerA.gameObject.scene);
                RoadKnotConnectionUndoSync.Watch(roadA, roadB);
                SplineSelection.Clear();
                Selection.activeGameObject = roadA.gameObject;
                // SplineSelection.Clear records its own selection-context Undo item.
                // Flush and collapse after it so restoring a selected-knot merge stays one transaction.
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
                SceneView.RepaintAll();
                mergedRoad = roadA;
                message = $"Merged into {roadA.name}: {plan.MergedSpline.Count} knots." +
                    (plan.Welded ? " Welded 1 coincident endpoint pair; all other knots retained." : " All source knots retained.") +
                    (containerB != containerA && !roadB.enabled ? " Empty road B was disabled; its GameObject and custom children remain." : " Other source splines remain intact.");
                return true;
            }
            catch (Exception exception)
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(group);
                if (roadA != null) roadA.RequestRebuild();
                if (roadB != null) roadB.RequestRebuild();
                message = "Merge cancelled; source roads restored. " + exception.Message;
                return false;
            }
        }

        internal static List<RoadKnotConnectionReference> SelectedKnots()
        {
            var infos = new List<SplineInfo>();
            foreach (GameObject target in Selection.gameObjects)
            {
                if (target == null || !target.TryGetComponent(out SplineContainer container)) continue;
                for (int i = 0; i < container.Splines.Count; i++) infos.Add(new SplineInfo(container, i));
            }
            var selected = new List<SelectableKnot>();
            SplineSelection.GetElements(infos, selected);
            var result = new List<RoadKnotConnectionReference>(selected.Count);
            foreach (SelectableKnot knot in selected)
            {
                if (!(knot.SplineInfo.Container is SplineContainer container)) continue;
                result.Add(new RoadKnotConnectionReference(container, knot.SplineInfo.Index, knot.KnotIndex));
            }
            return result;
        }
    }
}
