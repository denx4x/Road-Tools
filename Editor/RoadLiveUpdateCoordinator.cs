using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    // One authoring pipeline owns mesh + terrain + props. Derived terrain is recomputed
    // from its immutable base on Undo instead of adding a terrain Undo item every drag tick.
    [InitializeOnLoad]
    internal static class RoadLiveUpdateCoordinator
    {
        private const double RefreshInterval = 0.08;
        private sealed class RoadState
        {
            internal Matrix4x4 Matrix;
            internal string Status = "Live update ready.";
            internal string TerrainError;
        }
        private static readonly Dictionary<SplineRoad, RoadState> States = new();
        private static readonly HashSet<SplineRoad> Pending = new();
        private static readonly List<SplineRoad> Work = new();
        private static readonly HashSet<RoadTerrainManager> TerrainWork = new();
        private static bool processing;
        private static double nextRefresh;

        static RoadLiveUpdateCoordinator()
        {
            SplineRoad.AutoRebuildRequested += Queue;
            EditorApplication.update += Update;
            Undo.undoRedoEvent += OnUndoRedo;
            Undo.postprocessModifications += OnModifications;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            SceneView.duringSceneGui += OnSceneGUI;
            TerrainCallbacks.heightmapChanged += OnTerrainChanged;
            EditorApplication.delayCall += Discover;
        }

        internal static void Queue(SplineRoad road)
        {
            if (processing || EditorApplication.isPlayingOrWillChangePlaymode || road == null) return;
            if (!States.ContainsKey(road)) States.Add(road, new RoadState { Matrix = road.transform.localToWorldMatrix });
            if (road.LiveUpdatesEnabled && road.isActiveAndEnabled && !road.IsBaked) Pending.Add(road);
            EditorApplication.QueuePlayerLoopUpdate();
        }

        internal static string GetStatus(SplineRoad road) => road != null && States.TryGetValue(road, out var state)
            ? state.Status : "Live update ready.";

        internal static void QueueProfile(RoadProfile profile)
        {
            foreach (var pair in States)
                if (pair.Key != null && pair.Key.Profile == profile) Queue(pair.Key);
        }

        private static void Discover()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (SplineRoad road in Object.FindObjectsByType<SplineRoad>(FindObjectsSortMode.None)) Queue(road);
        }
        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => Discover();
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            Pending.Clear();
            if (state == PlayModeStateChange.EnteredEditMode) Discover();
        }
        private static void OnUndoRedo(in UndoRedoInfo info)
        {
            if (info.undoName == RoadMaterialUtility.MaterialUndoName || info.undoName == RoadMaterialSectionEditing.UndoName) return;
            Discover();
            EditorApplication.delayCall += FlushPending;
        }
        private static UndoPropertyModification[] OnModifications(UndoPropertyModification[] changes)
        {
            if (processing) return changes;
            foreach (var modification in changes)
            {
                if (RoadMaterialUtility.IsMaterialModification(modification.currentValue)) continue;
                Object target = modification.currentValue.target;
                if (target is RoadProfile profile)
                    QueueProfile(profile);
                else if (target is Component component)
                {
                    SplineRoad road = component.GetComponent<SplineRoad>();
                    if (road != null) Queue(road);
                }
            }
            return changes;
        }
        private static void OnTerrainChanged(Terrain terrain, RectInt area, bool synced)
        {
            if (processing || terrain == null) return;
            var manager = terrain.GetComponent<RoadTerrainManager>();
            foreach (var pair in States)
            {
                SplineRoad road = pair.Key;
                if (road == null || road.Profile == null || !road.Profile.ConformRoadToTerrain) continue;
                if (manager == null && Overlaps(road, terrain)) Queue(road);
            }
        }
        private static void OnSceneGUI(SceneView view)
        {
            if (Event.current.type == EventType.MouseUp && Pending.Count > 0)
                EditorApplication.delayCall += FlushPending;
        }
        private static void Update()
        {
            if (processing || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.timeSinceStartup < nextRefresh) return;
            Work.Clear();
            foreach (var pair in States)
            {
                if (pair.Key == null) { Work.Add(pair.Key); continue; }
                Matrix4x4 matrix = pair.Key.transform.localToWorldMatrix;
                if (matrix == pair.Value.Matrix) continue;
                pair.Value.Matrix = matrix; Queue(pair.Key);
            }
            foreach (SplineRoad destroyed in Work) { States.Remove(destroyed); Pending.Remove(destroyed); }
            FlushPending();
        }

        internal static void FlushPending()
        {
            if (processing || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || Pending.Count == 0) return;
            Work.Clear(); Work.AddRange(Pending); Pending.Clear(); TerrainWork.Clear();
            processing = true;
            try
            {
                foreach (SplineRoad road in Work)
                {
                    if (road == null || !road.LiveUpdatesEnabled || !road.isActiveAndEnabled || road.IsBaked || road.Profile == null) continue;
                    if (road.Profile.UsesTerrain || road.AdjustTerrainToRoad) EnsureTerrainRegistration(road, null, false);
                }
                RoadTerrainManager[] managers = Object.FindObjectsByType<RoadTerrainManager>(FindObjectsSortMode.None);
                foreach (SplineRoad road in Work)
                {
                    if (road == null || !road.LiveUpdatesEnabled || !road.isActiveAndEnabled || road.IsBaked || road.Profile == null) continue;
                    foreach (RoadTerrainManager manager in managers)
                    {
                        if (manager == null || manager.BaseSnapshot == null) continue;
                        foreach (SplineRoad listed in manager.Roads)
                            if (listed == road) { TerrainWork.Add(manager); break; }
                    }
                }
                foreach (RoadTerrainManager manager in TerrainWork)
                {
                    string result = manager.RebuildTerrainForLiveEdit();
                    var terrain = manager.GetComponent<Terrain>();
                    if (terrain != null && terrain.terrainData != null) EditorUtility.SetDirty(terrain.terrainData);
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                    foreach (SplineRoad road in manager.Roads)
                    {
                        if (road == null) continue;
                        if (States.TryGetValue(road, out var state)) state.Status = manager.LastRebuildSucceeded
                            ? "Live road, terrain and props updated." : "Live terrain: " + result;
                    }
                }
                foreach (SplineRoad road in Work)
                {
                    if (road == null || !road.LiveUpdatesEnabled || !road.isActiveAndEnabled || road.IsBaked || road.Profile == null) continue;
                    bool refreshed = false;
                    foreach (RoadTerrainManager manager in TerrainWork)
                        foreach (SplineRoad listed in manager.Roads)
                            if (listed == road && manager.LastRebuildSucceeded) refreshed = true;
                    if (!refreshed)
                    {
                        var report = road.Rebuild();
                        if (States.TryGetValue(road, out var state)) state.Status = report.Succeeded
                            ? "Live road and props updated." + (string.IsNullOrEmpty(state.TerrainError) ? "" : " " + state.TerrainError)
                            : report.Message;
                    }
                    EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                }
                SceneView.RepaintAll();
            }
            finally
            {
                processing = false;
                nextRefresh = EditorApplication.timeSinceStartup + RefreshInterval;
            }
        }

        internal static bool EnsureTerrainRegistration(SplineRoad road, Terrain explicitTerrain = null, bool recordUndo = true)
        {
            if (road == null || road.Profile == null || road.IsBaked) return false;
            if (States.TryGetValue(road, out var roadState)) roadState.TerrainError = null;
            bool registered = false;
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                if (terrain == null || terrain.terrainData == null ||
                    (explicitTerrain != null ? terrain != explicitTerrain : terrain.gameObject.scene != road.gameObject.scene || !Overlaps(road, terrain))) continue;
                RoadTerrainManager manager = terrain.GetComponent<RoadTerrainManager>();
                if (manager == null) manager = recordUndo
                    ? Undo.AddComponent<RoadTerrainManager>(terrain.gameObject)
                    : terrain.gameObject.AddComponent<RoadTerrainManager>();
                if (manager.BaseSnapshot == null) RoadTerrainManagerEditor.Capture(manager, terrain, recordUndo);
                if (manager.BaseSnapshot == null || !manager.BaseSnapshot.Matches(terrain.terrainData))
                {
                    if (States.TryGetValue(road, out var state)) state.TerrainError = state.Status = "Terrain snapshot does not match. Assign its original base snapshot.";
                    continue;
                }
                bool already = false;
                foreach (SplineRoad listed in manager.Roads) already |= listed == road;
                if (!already)
                {
                    // A derived rebuild (including Undo/Redo) must not add an authoring Undo item or clear Redo.
                    if (recordUndo) Undo.RecordObject(manager, "Connect Road to Terrain");
                    manager.AddRoad(road);
                    EditorUtility.SetDirty(manager); EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }
                if (!road.AdjustTerrainToRoad && !road.Profile.DeformTerrain)
                {
                    if (recordUndo) Undo.RecordObject(road, "Enable Live Road Terrain");
                    road.SetTerrainAdjustment(true); EditorUtility.SetDirty(road);
                }
                registered = true;
            }
            return registered;
        }
        private static bool Overlaps(SplineRoad road, Terrain terrain)
        {
            var container = road.GetComponent<SplineContainer>();
            if (container == null) return false;
            Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
            float width = road.Profile != null ? road.Profile.Width : 7f;
            foreach (Spline spline in container.Splines)
            {
                if (spline.Count == 0) continue;
                Bounds bounds = new Bounds(container.transform.TransformPoint((Vector3)spline[0].Position), Vector3.zero);
                foreach (BezierKnot knot in spline) bounds.Encapsulate(container.transform.TransformPoint((Vector3)knot.Position));
                bounds.Expand(width);
                if (bounds.max.x >= origin.x && bounds.min.x <= origin.x + size.x && bounds.max.z >= origin.z && bounds.min.z <= origin.z + size.z) return true;
            }
            return false;
        }
    }
}
