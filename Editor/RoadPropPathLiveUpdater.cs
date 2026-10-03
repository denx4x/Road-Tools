using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dyma.SplineLevelToolkit.Editor
{
    // Editor updates keep the last drag event visible even after ExecuteAlways Update stops.
    [InitializeOnLoad]
    internal static class RoadPropPathLiveUpdater
    {
        private const double RefreshInterval = 0.08;
        private static readonly HashSet<RoadPropPath> Pending = new();
        private static readonly List<RoadPropPath> Work = new();
        private static readonly HashSet<SplineRoad> PendingOwners = new();
        private static readonly List<SplineRoad> OwnerWork = new();
        private static double nextRefresh;
        private static bool processing;

        static RoadPropPathLiveUpdater()
        {
            RoadPropPath.AutoRebuildRequested += Queue;
            RoadPropPath.OwnerPropsInvalidated += QueueOwner;
            EditorApplication.update += Update;
            Undo.undoRedoPerformed += Discover;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += Discover;
        }

        private static void Queue(RoadPropPath path)
        {
            if (processing || path == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Pending.Count == 0 && PendingOwners.Count == 0) nextRefresh = EditorApplication.timeSinceStartup + RefreshInterval;
            Pending.Add(path);
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void QueueOwner(SplineRoad road)
        {
            if (processing || road == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Pending.Count == 0 && PendingOwners.Count == 0) nextRefresh = EditorApplication.timeSinceStartup + RefreshInterval;
            PendingOwners.Add(road);
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void Discover()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (RoadPropPath path in Object.FindObjectsByType<RoadPropPath>(FindObjectsSortMode.None))
                path.RequestRebuild();
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => Discover();

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            Pending.Clear();
            PendingOwners.Clear();
            if (state == PlayModeStateChange.EnteredEditMode) Discover();
        }

        private static void Update()
        {
            if (processing || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.timeSinceStartup < nextRefresh || (Pending.Count == 0 && PendingOwners.Count == 0)) return;
            Work.Clear();
            Work.AddRange(Pending);
            Pending.Clear();
            OwnerWork.Clear();
            OwnerWork.AddRange(PendingOwners);
            PendingOwners.Clear();
            processing = true;
            try
            {
                foreach (RoadPropPath path in Work)
                    if (path != null && path.FlushPending() && path.Owner != null)
                        EditorSceneManager.MarkSceneDirty(path.Owner.gameObject.scene);
                foreach (SplineRoad road in OwnerWork)
                {
                    if (road == null || !road.isActiveAndEnabled || !road.LiveUpdatesEnabled || road.IsBaked || road.Profile == null) continue;
                    road.RebuildProps();
                    EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                }
            }
            finally
            {
                processing = false;
                Work.Clear();
                OwnerWork.Clear();
                nextRefresh = EditorApplication.timeSinceStartup + RefreshInterval;
            }
            SceneView.RepaintAll();
        }
    }
}
