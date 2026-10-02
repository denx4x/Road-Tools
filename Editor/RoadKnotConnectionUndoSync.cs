using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    // Source containers/components participate in Undo; generated roots are derived data.
    // Redo can leave B disabled and empty after Undo rebuilt it, so sync only roads involved in a merge.
    [InitializeOnLoad]
    internal static class RoadKnotConnectionUndoSync
    {
        private static readonly HashSet<SplineRoad> Watched = new HashSet<SplineRoad>();
        private static readonly List<SplineRoad> Destroyed = new List<SplineRoad>();
        private static readonly string SessionKey = "RoadTools.MergeUndoRefs." + typeof(RoadKnotConnectionUndoSync).Assembly.GetName().Name;
        [Serializable] private sealed class SessionRoads { public List<SplineRoad> roads = new List<SplineRoad>(); }

        static RoadKnotConnectionUndoSync()
        {
            string stored = SessionState.GetString(SessionKey, "");
            if (!string.IsNullOrEmpty(stored))
            {
                SessionRoads session = JsonUtility.FromJson<SessionRoads>(stored);
                if (session?.roads != null)
                    foreach (SplineRoad road in session.roads) if (road != null) Watched.Add(road);
            }
            Undo.undoRedoEvent += OnUndoRedo;
        }

        internal static void Watch(SplineRoad a, SplineRoad b)
        {
            if (a != null) Watched.Add(a);
            if (b != null) Watched.Add(b);
            Persist();
        }

        private static void Persist()
        {
            // Let Unity serialize object references rather than assuming EntityId's numeric representation.
            var session = new SessionRoads();
            foreach (SplineRoad road in Watched) if (road != null) session.roads.Add(road);
            SessionState.SetString(SessionKey, JsonUtility.ToJson(session));
        }

        private static void OnUndoRedo(in UndoRedoInfo info)
        {
            if (info.undoName != RoadMaterialUtility.MaterialUndoName) Synchronize();
        }

        private static void Synchronize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Destroyed.Clear();
            foreach (SplineRoad road in Watched)
            {
                if (road == null) { Destroyed.Add(road); continue; }
                if (!road.gameObject.scene.IsValid() || !road.gameObject.scene.isLoaded) continue;
                SplineContainer container = road.GetComponent<SplineContainer>();
                if (container != null)
                {
                    // Unity's own spline inspector invalidates caches only for its currently selected targets.
                    // A merge can restore an unselected B container; rebuild its public wrapper/native cache too.
                    container.OnAfterDeserialize();
                    container.Splines = new List<Spline>(container.Splines);
                    foreach (Spline spline in container.Splines)
                    {
                        // Copy preserves knot metadata/tension, unlike the Spline copy constructor in 2.8.2.
                        // Copying equal authored data back invalidates local curve caches through the public API.
                        var snapshot = new Spline();
                        snapshot.Copy(spline);
                        spline.Copy(snapshot);
                    }
                }
                if (container != null && container.Splines.Count == 0 && !road.IsBaked)
                {
                    road.GetComponent<RoadMeshGenerator>()?.ClearGenerated();
                    road.GetComponent<PropLayerManager>()?.ClearGenerated();
                }
                else if (road.isActiveAndEnabled && road.LiveUpdatesEnabled && !road.IsBaked)
                    road.RequestRebuild();
            }
            foreach (SplineRoad road in Destroyed) Watched.Remove(road);
            if (Destroyed.Count > 0) Persist();
        }
    }
}
