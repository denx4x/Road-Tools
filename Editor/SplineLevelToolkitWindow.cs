using UnityEditor;
using UnityEditor.Splines;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Dyma.SplineLevelToolkit.Editor
{
    public sealed class SplineLevelToolkitWindow : EditorWindow
    {
        private RoadProfile profile;
        [SerializeField] private Material newRoadMaterial;
        private RoadSocket socket;
        private Terrain targetTerrain;
        private Vector2 scrollPosition;
        private string actionStatus;
        private MessageType actionStatusType;
        private int activeTab;
        [SerializeField] private float directionSegmentLength = 10f;
        [SerializeField] private float customDirectionAngle = 30f;
        [SerializeField] private float turnRadius = 12f;
        [SerializeField] private bool showTurnPreview;
        private readonly RoadTurnPreview turnPreview = new RoadTurnPreview();
        [SerializeField] private RoadKnotConnectionPanel knotConnections = new RoadKnotConnectionPanel();
        private static readonly string[] TabNames = { "Road", "Props", "Terrain", "Connections", "Test Scene" };

        [MenuItem("Tools/Road Tools/Open Window")]
        public static void Open() => GetWindow<SplineLevelToolkitWindow>("Road Tools");

        private void OnEnable()
        {
            RoadToolsWorkspace.EnsureProjectFolders();
            if (knotConnections == null) knotConnections = new RoadKnotConnectionPanel();
            titleContent = new GUIContent("Road Tools");
            LoadDefaultProfileIfMissing();
            SplineSelection.changed += Repaint;
            SplineSelection.changed += ObserveConnectionSelection;
            SceneView.duringSceneGui += DrawTurnPreview;
            TerrainCallbacks.heightmapChanged += OnTerrainHeightChanged;
        }

        private void OnDisable()
        {
            SplineSelection.changed -= Repaint;
            SplineSelection.changed -= ObserveConnectionSelection;
            SceneView.duringSceneGui -= DrawTurnPreview;
            TerrainCallbacks.heightmapChanged -= OnTerrainHeightChanged;
            turnPreview.Clear();
            SceneView.RepaintAll();
        }

        private void OnTerrainHeightChanged(Terrain terrain, RectInt region, bool synced)
        {
            turnPreview.Invalidate();
            Repaint(); SceneView.RepaintAll();
        }

        private void DrawTurnPreview(SceneView view)
        {
            if (activeTab == 3) knotConnections.DrawPreview();
            if (!showTurnPreview || activeTab != 0) { turnPreview.Clear(); return; }
            var road = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<SplineRoad>() : null;
            turnPreview.Update(road, directionSegmentLength, customDirectionAngle, turnRadius);
            turnPreview.Draw();
        }

        private void ObserveConnectionSelection()
        {
            if (activeTab == 3) knotConnections.ObserveSelection();
        }

        private void OnGUI()
        {
            GameObject selected = Selection.activeGameObject;
            SplineContainer selectedSpline = selected != null ? selected.GetComponent<SplineContainer>() : null;
            SplineRoad selectedRoad = selected != null ? selected.GetComponent<SplineRoad>() : null;

            DrawHeader(selectedRoad);
            int previousTab = activeTab;
            activeTab = GUILayout.Toolbar(activeTab, TabNames, GUILayout.Height(28));
            if (activeTab == 3 && previousTab != activeTab) knotConnections.ObserveSelection();
            if (!string.IsNullOrEmpty(actionStatus))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(actionStatus, actionStatusType);
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.Space(8);
            switch (activeTab)
            {
                case 0: DrawRoadTab(selected, selectedSpline, selectedRoad); break;
                case 1: DrawPropsTab(selectedRoad); break;
                case 2: DrawTerrainTab(selectedRoad); break;
                case 3: DrawConnectionsTab(selectedRoad); break;
                case 4: DrawTestSceneTab(); break;
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader(SplineRoad selectedRoad)
        {
            EditorGUILayout.BeginVertical(RoadToolsWindowStyles.Header);
            EditorGUILayout.LabelField("ROAD TOOLS", RoadToolsWindowStyles.Title);
            EditorGUILayout.LabelField(
                selectedRoad != null ? $"Selected: {selectedRoad.name}" : "Create and shape spline roads",
                RoadToolsWindowStyles.Subtitle);
            EditorGUILayout.EndVertical();
        }

        private void DrawRoadTab(GameObject selected, SplineContainer selectedSpline, SplineRoad selectedRoad)
        {
            BeginCard("SELECTED ROAD");
            if (selectedRoad == null)
            {
                EditorGUILayout.LabelField("Select a road in the Hierarchy to edit or bake it.", RoadToolsWindowStyles.Body);
            }
            else if (selectedRoad.IsBaked)
            {
                if (GUILayout.Button("Resume Editing", GUILayout.Height(32)))
                {
                    RoadBuildReport result = selectedRoad.ResumeEditing();
                    if (result.Succeeded)
                        EditorSceneManager.MarkSceneDirty(selectedRoad.gameObject.scene);
                    SetStatus(result.Message, result.Succeeded);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Live editing updates road, terrain and props as you move or rotate spline points.", MessageType.Info);
                EditorGUILayout.LabelField(RoadLiveUpdateCoordinator.GetStatus(selectedRoad), EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Auto Fix Clipping", GUILayout.Height(32)))
                {
                    RoadClippingFixReport result = RoadClippingFixer.Fix(selectedRoad);
                    SetStatus(
                        $"Adjusted {result.AdjustedKnots} knots; smoothed {result.SmoothedKnots} bends. All knots preserved. {result.Build.Message}" +
                        (result.RemainingTightCorner ? " Some overlap or tight curvature remains. Adjust the route or width." : ""),
                        result.Succeeded);
                }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh Now (Recovery)", GUILayout.Height(24)))
                {
                    RoadBuildReport result = selectedRoad.Rebuild();
                    if (result.Succeeded)
                        EditorSceneManager.MarkSceneDirty(selectedRoad.gameObject.scene);
                    SetStatus(result.Message, result.Succeeded);
                }
                if (GUILayout.Button("Bake Meshes", GUILayout.Height(28)))
                {
                    RoadBakeReport result = RoadBakeUtility.Bake(selectedRoad);
                    SetStatus(result.Message, result.Succeeded);
                    if (result.Succeeded)
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<DefaultAsset>(result.Folder));
                }
                EditorGUILayout.EndHorizontal();
            }
            if (selectedRoad != null && SplineSelection.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Point Before")) InsertKnots(false);
                if (GUILayout.Button("+ Point After")) InsertKnots(true);
                EditorGUILayout.EndHorizontal();
            }
            if (selectedRoad != null && !selectedRoad.IsBaked)
                DrawDirectionPresets(selectedRoad);
            EndCard();

            if (selectedRoad != null)
            {
                BeginCard("ROAD MATERIAL");
                RoadToolsMaterialPanel.Draw(selectedRoad);
                EndCard();
            }

            BeginCard("CREATE ROAD");
            profile = (RoadProfile)EditorGUILayout.ObjectField("Road Profile", profile, typeof(RoadProfile), false);
            RoadToolsMaterialPanel.DrawTemplate(ref newRoadMaterial, profile);
            if (GUILayout.Button("Create Road From Scene View", GUILayout.Height(30)))
            {
                SplineRoad created = CreateRoad(profile);
                if (created != null && newRoadMaterial != null) RoadMaterialUtility.Set(created, newRoadMaterial);
            }
            if (profile == null)
                EditorGUILayout.LabelField("An existing or default Road Profile will be assigned automatically.", RoadToolsWindowStyles.Body);
            if (selected != null)
            {
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Set Up Road Tools on Selected GameObject", GUILayout.Height(28)))
                {
                    SplineRoad configured = RoadSetupUtility.EnsureRoad(selected, profile, targetTerrain);
                    SetStatus(configured != null ? "Road components, profile and live updates are ready." : "Select a scene GameObject.", configured != null);
                }
            }
            if (selectedSpline != null)
            {
                if (GUILayout.Button("Add Spline to Selected Container"))
                    SplineRoadSceneGUI.AddSpline(selectedSpline, selected.GetComponent<SplineRoad>());
            }
            EndCard();

            BeginCard("MORE ACTIONS");
            if (selectedRoad != null && GUILayout.Button("Edit Prop Layers"))
                activeTab = 1;
            if (GUILayout.Button("Rebuild All Roads"))
                RebuildAll();
            EndCard();
        }

        private void DrawPropsTab(SplineRoad selectedRoad)
        {
            BeginCard("ROAD PROPS");
            RoadToolsPropsPanel.Draw(selectedRoad);
            EndCard();
        }

        private void DrawDirectionPresets(SplineRoad road)
        {
            EditorGUILayout.Space(9);
            EditorGUILayout.LabelField("ROAD DIRECTION", RoadToolsWindowStyles.SectionTitle);
            if (road.Profile == null)
            {
                EditorGUILayout.HelpBox("Assign a Road Profile before previewing a turn.", MessageType.Info);
                return;
            }
            if (!RoadDirectionPresetTool.TryGetSelectedKnot(road, out _, out int knotIndex))
            {
                EditorGUILayout.LabelField("Select one road point in Scene View to set the next segment direction.",
                    RoadToolsWindowStyles.Body);
                return;
            }

            EditorGUILayout.LabelField($"From selected point P{knotIndex + 1}: extend at the end, or move the next point.",
                RoadToolsWindowStyles.Body);
            directionSegmentLength = Mathf.Max(0.5f, EditorGUILayout.FloatField("Segment Length (m)", directionSegmentLength));
            turnRadius = EditorGUILayout.FloatField("Bend Radius (m)", turnRadius);
            EditorGUILayout.LabelField($"Minimum radius: {road.Profile.Width * 0.5f + 0.5f:0.##} m. Length follows the bend.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Straight (0°)", GUILayout.Height(27)))
                ApplyDirection(road, 0f);
            DrawDirectionRow(road, 30f, 45f);
            DrawDirectionRow(road, 60f, 90f);
            EditorGUILayout.BeginHorizontal();
            customDirectionAngle = EditorGUILayout.FloatField("Custom Angle (°)", customDirectionAngle);
            EditorGUILayout.EndHorizontal();
            showTurnPreview = EditorGUILayout.Toggle("Show Turn Preview", showTurnPreview);
            if (showTurnPreview)
            {
                turnPreview.Update(road, directionSegmentLength, customDirectionAngle, turnRadius);
                EditorGUILayout.HelpBox(turnPreview.Plan?.Message ?? "Select a point.",
                    turnPreview.Plan != null && turnPreview.Plan.Safe ? MessageType.Info : MessageType.Warning);
            }
            using (new EditorGUI.DisabledScope(!showTurnPreview || turnPreview.Plan == null || !turnPreview.Plan.Safe))
                if (GUILayout.Button("Apply Previewed Turn", GUILayout.Height(30)))
                {
                    bool success = RoadDirectionPresetTool.Apply(road, directionSegmentLength, customDirectionAngle, turnRadius, out string result);
                    SetStatus(result, success);
                    if (success) { showTurnPreview = false; turnPreview.Clear(); }
                    SceneView.RepaintAll();
                }
            if (GUI.changed) SceneView.RepaintAll();
        }

        private void DrawDirectionRow(SplineRoad road, float first, float second)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"Left {first:0}°")) ApplyDirection(road, -first);
            if (GUILayout.Button($"Right {first:0}°")) ApplyDirection(road, first);
            if (GUILayout.Button($"Left {second:0}°")) ApplyDirection(road, -second);
            if (GUILayout.Button($"Right {second:0}°")) ApplyDirection(road, second);
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyDirection(SplineRoad road, float angleDegrees)
        {
            customDirectionAngle = angleDegrees;
            showTurnPreview = true;
            turnPreview.Update(road, directionSegmentLength, customDirectionAngle, turnRadius);
            SceneView.RepaintAll();
        }

        private void DrawTerrainTab(SplineRoad selectedRoad)
        {
            BeginCard("TERRAIN SETUP");
            targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", targetTerrain, typeof(Terrain), true);
            if (targetTerrain == null)
                targetTerrain = Terrain.activeTerrain;
            if (targetTerrain == null)
                EditorGUILayout.LabelField("Choose a Terrain to enable road terrain tools.", RoadToolsWindowStyles.Body);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Fit the selected road to terrain height and blend the ground along its path.", RoadToolsWindowStyles.Body);
            using (new EditorGUI.DisabledScope(targetTerrain == null || selectedRoad == null || selectedRoad.IsBaked))
            {
                if (GUILayout.Button("Auto Adjust Road + Terrain", GUILayout.Height(32)))
                {
                    bool succeeded = RoadTerrainAutoAdjuster.TryAdjust(selectedRoad, targetTerrain, out string result);
                    SetStatus(result, succeeded);
                }
            }
            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(targetTerrain == null))
            {
                if (GUILayout.Button("Set Up Terrain Manager", GUILayout.Height(28)))
                {
                    RoadTerrainManager manager = targetTerrain.GetComponent<RoadTerrainManager>();
                    if (manager == null)
                        manager = Undo.AddComponent<RoadTerrainManager>(targetTerrain.gameObject);
                    Selection.activeGameObject = manager.gameObject;
                    SetStatus("Terrain manager ready. Capture its base snapshot in the Inspector.", true);
                }
            }
            RoadTerrainManager targetManager = targetTerrain != null
                ? targetTerrain.GetComponent<RoadTerrainManager>() : null;
            using (new EditorGUI.DisabledScope(targetManager == null || selectedRoad == null))
            {
                if (GUILayout.Button("Add Selected Road to Terrain"))
                {
                    Undo.RecordObject(targetManager, "Add Road Terrain Modifier");
                    bool added = targetManager.AddRoad(selectedRoad);
                    EditorUtility.SetDirty(targetManager);
                    if (added)
                        EditorSceneManager.MarkSceneDirty(targetManager.gameObject.scene);
                    SetStatus(added ? "Road added to terrain modifier list." : "Road is already in the terrain modifier list.", true);
                }
            }
            RoadProfile terrainProfile = selectedRoad != null ? selectedRoad.Profile : null;
            if (RoadToolsPackagePaths.IsPackageAsset(terrainProfile))
            {
                EditorGUILayout.HelpBox("This package preset is read-only. Create an editable copy for this road before changing its terrain bands.", MessageType.Info);
                if (GUILayout.Button("Create Editable Road Profile"))
                {
                    Undo.RecordObject(selectedRoad, "Create Editable Road Profile");
                    terrainProfile = RoadSetupUtility.CreateEditableProfile(terrainProfile);
                    selectedRoad.SetProfile(terrainProfile);
                    EditorUtility.SetDirty(selectedRoad);
                    EditorSceneManager.MarkSceneDirty(selectedRoad.gameObject.scene);
                }
            }
            bool bandsReordered;
            using (new EditorGUI.DisabledScope(RoadToolsPackagePaths.IsPackageAsset(terrainProfile)))
                bandsReordered = RoadToolsTerrainBandsPanel.Draw(terrainProfile);
            if (bandsReordered)
            {
                bool roadRegistered = false;
                if (targetManager != null)
                    foreach (SplineRoad listedRoad in targetManager.Roads)
                        roadRegistered |= listedRoad == selectedRoad;

                if (targetManager == null || targetManager.BaseSnapshot == null || !roadRegistered)
                    SetStatus("Band order saved. Select a Terrain with a base snapshot and register this road to preview it.", false);
                else
                {
                    RoadLiveUpdateCoordinator.Queue(selectedRoad);
                    SetStatus("Terrain bands saved; live terrain update queued.", true);
                }
            }
            using (new EditorGUI.DisabledScope(targetManager == null || targetManager.BaseSnapshot == null))
            {
                if (GUILayout.Button("Rebuild Terrain From Base"))
                {
                    Undo.RegisterCompleteObjectUndo(targetTerrain.terrainData, "Rebuild Road Terrain");
                    string result = targetManager.RebuildTerrain();
                    EditorUtility.SetDirty(targetTerrain.terrainData);
                    EditorSceneManager.MarkSceneDirty(targetTerrain.gameObject.scene);
                    SetStatus(result, targetManager.LastRebuildSucceeded);
                }
            }

            EndCard();
        }

        private void DrawConnectionsTab(SplineRoad selectedRoad)
        {
            BeginCard("MERGE TWO ROADS");
            knotConnections.Draw(SetStatus);
            EndCard();
            BeginCard("INTERSECTIONS");
            if (GUILayout.Button("Create 4-Way Socket Hub", GUILayout.Height(27)))
                CreateSocketHub();
            socket = (RoadSocket)EditorGUILayout.ObjectField("Target Socket", socket, typeof(RoadSocket), true);
            bool selectedRoadForSocket = selectedRoad != null && socket != null && !socket.Occupied;
            if (socket != null && socket.Occupied)
                EditorGUILayout.HelpBox("Socket ini sudah terhubung. Pilih socket kosong untuk road berikutnya.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(!selectedRoadForSocket))
            {
                if (GUILayout.Button("Snap Nearest Road Endpoint", GUILayout.Height(26)))
                {
                    string result = RoadEndpointSnapUtility.Snap(selectedRoad, socket);
                    SetStatus(result, result.StartsWith("Snapped"));
                }
            }

            EndCard();
        }

        private void DrawTestSceneTab()
        {
            BeginCard("PROJECT FOLDERS & SAMPLES");
            EditorGUILayout.LabelField("Workspace: Assets/Road Tools", RoadToolsWindowStyles.Body);
            EditorGUILayout.LabelField("Samples, Documentation, Generated and Development are ready for your project.", RoadToolsWindowStyles.Body);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Project Folder")) RoadToolsWorkspace.OpenProjectFolder();
            if (GUILayout.Button("Open Documentation")) RoadToolsWorkspace.OpenDocumentation();
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Import Demo Sample", GUILayout.Height(30)))
            {
                RoadToolsSampleImporter.ImportDemo(out string message, out MessageType type);
                actionStatus = message;
                actionStatusType = type;
                Repaint();
            }
            EndCard();
            BeginCard("TEST SCENE");
            EditorGUILayout.LabelField("Choose where to set up the road test environment.", RoadToolsWindowStyles.Body);
            EditorGUILayout.Space(6);
            if (GUILayout.Button("Create New Test Scene", GUILayout.Height(30)))
                RoadToolsTestSceneBuilder.BuildNewScene();
            if (GUILayout.Button("Set Up Current Scene", GUILayout.Height(30)))
                RoadToolsTestSceneBuilder.BuildInCurrentScene();
            EndCard();
        }

        private static void BeginCard(string heading)
        {
            EditorGUILayout.BeginVertical(RoadToolsWindowStyles.Card);
            EditorGUILayout.LabelField(heading, RoadToolsWindowStyles.SectionTitle);
            EditorGUILayout.Space(5);
        }

        private static void EndCard()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(7);
        }

        public static SplineRoad CreateRoad(RoadProfile selectedProfile)
        {
            if (selectedProfile == null)
                selectedProfile = RoadSetupUtility.ResolveDefaultProfile();
            if (selectedProfile == null)
            {
                Debug.LogError("Road Tools requires a Road Profile. Create one from Assets > Create > Road Tools > Road Profile.");
                return null;
            }

            var roadObject = new GameObject("Spline Road");
            Undo.RegisterCreatedObjectUndo(roadObject, "Create Spline Road");
            PlaceAtSceneView(roadObject.transform);
            var container = roadObject.AddComponent<SplineContainer>();
            container.Spline.Clear();
            container.Spline.Add(new BezierKnot(new float3(0f, 0f, -10f)));
            container.Spline.Add(new BezierKnot(new float3(0f, 0f, 0f)));
            container.Spline.Add(new BezierKnot(new float3(0f, 0f, 10f)));
            for (int i = 0; i < container.Spline.Count; i++)
                container.Spline.SetTangentMode(i, TangentMode.AutoSmooth);
            SplineRoad road = RoadSetupUtility.EnsureRoad(roadObject, selectedProfile);
            Selection.activeGameObject = roadObject;
            EditorSceneManager.MarkSceneDirty(roadObject.scene);
            return road;
        }

        private void LoadDefaultProfileIfMissing()
        {
            if (profile == null)
                profile = FindAvailableProfile();
        }

        private static RoadProfile FindAvailableProfile()
        {
            RoadProfile defaultProfile = RoadToolsPackagePaths.LoadDefault<RoadProfile>("Profiles/Two Lane Asphalt.asset");
            if (defaultProfile != null)
                return defaultProfile;
            string[] guids = AssetDatabase.FindAssets("t:RoadProfile");
            System.Array.Sort(guids);
            foreach (string guid in guids)
            {
                RoadProfile found = AssetDatabase.LoadAssetAtPath<RoadProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void AddRoadToSelectedSpline(RoadProfile selectedProfile)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null || !selected.TryGetComponent(out SplineContainer _))
                return;

            SplineRoad road = RoadSetupUtility.EnsureRoad(selected, selectedProfile);
            EditorSceneManager.MarkSceneDirty(selected.scene);
            EditorGUIUtility.PingObject(road);
        }

        private void CreateSocketHub()
        {
            const string prefabFolder = RoadToolsPackagePaths.GeneratedRoot + "/Prefabs";
            const string prefabPath = prefabFolder + "/4-Way Socket Hub.prefab";
            GameObject existingPrefab = RoadToolsPackagePaths.LoadDefault<GameObject>("Prefabs/4-Way Socket Hub.prefab") ??
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(existingPrefab);
                Undo.RegisterCreatedObjectUndo(instance, "Create Road Socket Hub");
                PlaceAtSceneView(instance.transform);
                socket = instance.GetComponentInChildren<RoadSocket>();
                Selection.activeGameObject = instance;
                EditorSceneManager.MarkSceneDirty(instance.scene);
                SetStatus("Created 4-way intersection prefab instance.", true);
                return;
            }

            var hub = new GameObject("Road Socket Hub");
            Undo.RegisterCreatedObjectUndo(hub, "Create Road Socket Hub");
            PlaceAtSceneView(hub.transform);
            float armLength = profile != null ? Mathf.Max(4f, profile.Width * 0.5f) : 4f;
            RoadSocket firstSocket = null;

            for (int i = 0; i < 4; i++)
            {
                var socketObject = new GameObject($"Socket {i + 1}");
                Undo.RegisterCreatedObjectUndo(socketObject, "Create Road Socket");
                socketObject.transform.SetParent(hub.transform, false);
                socketObject.transform.localPosition = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward * armLength;
                socketObject.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                RoadSocket createdSocket = socketObject.AddComponent<RoadSocket>();
                if (firstSocket == null)
                    firstSocket = createdSocket;
            }

            hub.AddComponent<RoadIntersection>().RefreshSockets();
            socket = firstSocket;
            RoadSetupUtility.EnsureAssetFolder(prefabFolder);
            Vector3 placedPosition = hub.transform.position;
            Quaternion placedRotation = hub.transform.rotation;
            hub.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            PrefabUtility.SaveAsPrefabAssetAndConnect(hub, prefabPath, InteractionMode.AutomatedAction);
            hub.transform.SetPositionAndRotation(placedPosition, placedRotation);
            Selection.activeGameObject = hub;
            EditorSceneManager.MarkSceneDirty(hub.scene);
            SceneView.lastActiveSceneView?.FrameSelected();
            SetStatus($"Created reusable intersection prefab at {prefabPath}.", true);
        }

        private static void PlaceAtSceneView(Transform target)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
                return;

            Vector3 direction = sceneView.camera.transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector3.forward;

            target.position = sceneView.pivot;
            target.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static void InsertKnots(bool after)
        {
            var knots = new List<SelectableKnot>();
            var splines = new List<SplineInfo>();
            foreach (GameObject selected in Selection.gameObjects)
            {
                if (!selected.TryGetComponent(out SplineContainer container))
                    continue;

                for (int i = 0; i < container.Splines.Count; i++)
                    splines.Add(new SplineInfo(container, i));
            }

            SplineSelection.GetElements(splines, knots);
            knots.Sort((a, b) => b.KnotIndex.CompareTo(a.KnotIndex));
            var changedContainers = new HashSet<SplineContainer>();

            foreach (SelectableKnot selectedKnot in knots)
            {
                var container = selectedKnot.SplineInfo.Container as SplineContainer;
                Spline spline = selectedKnot.SplineInfo.Spline;
                if (container == null || spline == null || spline.Count < 2)
                    continue;

                int index = selectedKnot.KnotIndex;
                int insertIndex;
                BezierKnot newKnot;
                if (after && index == spline.Count - 1)
                {
                    BezierKnot endpoint = spline[index];
                    float3 forward = math.mul(endpoint.Rotation, new float3(0f, 0f, 1f));
                    float3 tangentOffset = forward * 2f;
                    insertIndex = spline.Count;
                    newKnot = new BezierKnot(endpoint.Position + forward * 5f, -tangentOffset, tangentOffset, endpoint.Rotation);
                }
                else if (!after && index == 0)
                {
                    BezierKnot endpoint = spline[index];
                    float3 backward = math.mul(endpoint.Rotation, new float3(0f, 0f, -1f));
                    float3 tangentOffset = -backward * 2f;
                    insertIndex = 0;
                    newKnot = new BezierKnot(endpoint.Position + backward * 5f, -tangentOffset, tangentOffset, endpoint.Rotation);
                }
                else
                {
                    int leftIndex = after ? index : index - 1;
                    insertIndex = after ? index + 1 : index;
                    float t = (leftIndex + 0.5f) / (spline.Count - 1f);
                    float3 position = SplineUtility.EvaluatePosition(spline, t);
                    float3 tangent = SplineUtility.EvaluateTangent(spline, t);
                    float3 tangentOffset = math.normalizesafe(tangent) * (math.length(tangent) * 0.25f);
                    newKnot = new BezierKnot(position, -tangentOffset, tangentOffset, quaternion.identity);
                }

                Undo.RecordObject(container, after ? "Add Spline Point After" : "Add Spline Point Before");
                spline.Insert(insertIndex, newKnot);
                spline.SetTangentMode(insertIndex, TangentMode.Mirrored);
                EditorUtility.SetDirty(container);
                changedContainers.Add(container);
            }

            foreach (SplineRoad road in Object.FindObjectsByType<SplineRoad>(FindObjectsSortMode.None))
            {
                if (changedContainers.Contains(road.GetComponent<SplineContainer>()))
                    road.Rebuild();
            }
            if (changedContainers.Count > 0)
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private void RebuildAll()
        {
            int rebuilt = 0;
            int failed = 0;
            foreach (SplineRoad road in Object.FindObjectsByType<SplineRoad>(FindObjectsSortMode.None))
            {
                RoadBuildReport result = road.Rebuild();
                if (result.Succeeded)
                {
                    rebuilt++;
                    EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                }
                else
                {
                    failed++;
                }
            }

            SetStatus($"Rebuilt {rebuilt} roads. {failed} roads need attention or are baked.", failed == 0);
        }

        private void SetStatus(string message, bool succeeded)
        {
            actionStatus = message;
            actionStatusType = succeeded ? MessageType.Info : MessageType.Error;
            Repaint();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

    }
}
