using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadKnotJunctionUtility
    {
        internal static bool Join(RoadKnotConnectionReference a, RoadKnotConnectionReference b,
            out SplineRoad joinedRoad, out string message)
        {
            joinedRoad = null;
            if (!RoadKnotJunctionGeometry.Validate(a, b, out message)) return false;
            SplineContainer containerA = a.Container, containerB = b.Container;
            SplineRoad roadA = containerA.GetComponent<SplineRoad>(), roadB = containerB.GetComponent<SplineRoad>();
            int destinationSpline = b.SplineIndex;
            Vector3 destination = b.Position;
            Vector3 origin = a.Position;
            bool hasBridge = RoadKnotJunctionBridge.NeedsBridge(origin, destination);
            var existingChildren = new HashSet<Transform>();
            foreach (Transform child in roadA.transform) existingChildren.Add(child);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Join Road Knots");
            RoadJunctionSetupUtility.Ensure(roadA);
            Undo.RecordObject(containerA, "Join Road Knots");
            Undo.RegisterCompleteObjectUndo(roadA, "Join Road Knots");
            if (containerA != containerB)
            {
                Undo.RecordObject(containerB, "Join Road Knots");
                Undo.RegisterCompleteObjectUndo(roadB, "Join Road Knots");
            }
            try
            {
                if (containerA != containerB)
                {
                    int offset = containerA.Splines.Count;
                    RoadKnotJunctionMaterials.Transfer(roadB, roadA, offset);
                    // AddComponent can flush Undo recording; complete setup before
                    // changing either spline array, then resume container tracking.
                    Undo.RecordObject(containerA, "Join Road Knots");
                    Undo.RecordObject(containerB, "Join Road Knots");
                    var splines = new List<Spline>(containerA.Splines);
                    Matrix4x4 matrix = containerA.transform.worldToLocalMatrix * containerB.transform.localToWorldMatrix;
                    foreach (Spline source in containerB.Splines)
                        splines.Add(RoadKnotJunctionGeometry.CopyTransformed(source, matrix));
                    containerA.Splines = splines;
                    CopyLinks(containerB, containerA, offset);
                    destinationSpline += offset;
                }
                var indexA = new SplineKnotIndex(a.SplineIndex, a.KnotIndex);
                var indexB = new SplineKnotIndex(destinationSpline, b.KnotIndex);
                if (hasBridge)
                {
                    int bridgeIndex = containerA.Splines.Count;
                    var branches = new List<Spline>(containerA.Splines)
                    { RoadKnotJunctionBridge.Create(containerA, origin, destination) };
                    containerA.Splines = branches;
                    containerA.LinkKnots(indexA, new SplineKnotIndex(bridgeIndex, 0));
                    containerA.LinkKnots(indexB, new SplineKnotIndex(bridgeIndex, 1));
                }
                else
                {
                    Spline splineA = containerA.Splines[a.SplineIndex];
                    BezierKnot knot = splineA[a.KnotIndex];
                    knot.Position = containerA.transform.InverseTransformPoint(destination);
                    splineA[a.KnotIndex] = knot;
                    containerA.LinkKnots(indexA, indexB);
                    containerA.SetLinkedKnotPosition(indexA);
                }
                RoadBuildReport build = roadA.Rebuild();
                if (!build.Succeeded) throw new InvalidOperationException(build.Message);

                if (containerA != containerB)
                {
                    for (int i = containerB.Splines.Count - 1; i >= 0; i--) containerB.RemoveSplineAt(i);
                    roadB.GetComponent<RoadMeshGenerator>()?.ClearGenerated();
                    roadB.GetComponent<PropLayerManager>()?.ClearGenerated();
                    roadB.enabled = false;
                    foreach (RoadTerrainManager manager in UnityEngine.Object.FindObjectsByType<RoadTerrainManager>(FindObjectsSortMode.None))
                    {
                        bool registered = false;
                        foreach (SplineRoad road in manager.Roads) registered |= road == roadB;
                        if (!registered) continue;
                        Undo.RecordObject(manager, "Join Road Knots");
                        manager.RemoveRoad(roadB);
                        manager.InvalidateLiveEditCache();
                        EditorUtility.SetDirty(manager);
                    }
                    EditorUtility.SetDirty(containerB);
                    EditorUtility.SetDirty(roadB);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(containerB);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(roadB);
                }
                EditorUtility.SetDirty(containerA);
                EditorUtility.SetDirty(roadA);
                PrefabUtility.RecordPrefabInstancePropertyModifications(containerA);
                PrefabUtility.RecordPrefabInstancePropertyModifications(roadA);
                RoadLiveUpdateCoordinator.EnsureTerrainRegistration(roadA);
                roadA.RequestRebuild();
                RoadKnotConnectionUndoSync.Watch(roadA, roadB);
                EditorSceneManager.MarkSceneDirty(containerA.gameObject.scene);
                SplineSelection.Clear();
                Selection.activeGameObject = roadA.gameObject;
                Undo.FlushUndoRecordObjects();
                foreach (Transform child in roadA.transform)
                    if (!existingChildren.Contains(child))
                        Undo.RegisterCreatedObjectUndo(child.gameObject, "Join Road Knots");
                Undo.CollapseUndoOperations(group);
                SceneView.RepaintAll();
                joinedRoad = roadA;
                message = $"Joined knots on {roadA.name}. All spline branches and knots retained; shared knots move together. " +
                    (hasBridge ? "Added an editable connector; source knots were not moved. " : "Coincident knots linked. ") +
                    (containerA != containerB ? "All source B splines were transferred; B's GameObject and custom children remain." : "No source spline was removed.");
                return true;
            }
            catch (Exception exception)
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(group);
                if (roadA != null) roadA.RequestRebuild();
                if (roadB != null) roadB.RequestRebuild();
                message = "Join cancelled; source roads restored. " + exception.Message;
                return false;
            }
        }

        private static void CopyLinks(SplineContainer source, SplineContainer destination, int offset)
        {
            for (int s = 0; s < source.Splines.Count; s++)
                for (int k = 0; k < source.Splines[s].Count; k++)
                {
                    var index = new SplineKnotIndex(s, k);
                    if (!source.KnotLinkCollection.TryGetKnotLinks(index, out var links)) continue;
                    foreach (SplineKnotIndex linked in links)
                    {
                        if (linked.Spline < s || (linked.Spline == s && linked.Knot <= k)) continue;
                        destination.KnotLinkCollection.Link(new SplineKnotIndex(s + offset, k),
                            new SplineKnotIndex(linked.Spline + offset, linked.Knot));
                    }
                }
        }

        internal static bool CanUnlink(RoadKnotConnectionReference knot) => knot != null &&
            knot.TryResolve(out _, out _, out _) &&
            !knot.Container.GetComponent<SplineRoad>().IsBaked &&
            knot.Container.KnotLinkCollection.TryGetKnotLinks(new SplineKnotIndex(knot.SplineIndex, knot.KnotIndex), out var links) && links.Count > 1;

        internal static bool Unlink(RoadKnotConnectionReference knot, out string message)
        {
            message = "Select one shared knot to unlink.";
            if (!CanUnlink(knot)) return false;
            Undo.RecordObject(knot.Container, "Unlink Road Knot");
            knot.Container.UnlinkKnots(new[] { new SplineKnotIndex(knot.SplineIndex, knot.KnotIndex) });
            EditorUtility.SetDirty(knot.Container);
            PrefabUtility.RecordPrefabInstancePropertyModifications(knot.Container);
            EditorSceneManager.MarkSceneDirty(knot.Container.gameObject.scene);
            RoadKnotConnectionUndoSync.Watch(knot.Container.GetComponent<SplineRoad>(), null);
            knot.Container.GetComponent<SplineRoad>().RequestRebuild();
            Undo.FlushUndoRecordObjects();
            SceneView.RepaintAll();
            message = "Selected knot unlinked. Its position and spline branches are retained.";
            return true;
        }
    }
}
