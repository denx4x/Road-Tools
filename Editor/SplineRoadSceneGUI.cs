using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class SplineRoadSceneGUI
    {
        private const int CurveStepsPerSegment = 16;
        private static GUIStyle addPointButtonStyle;

        public static void Draw(SplineRoad road)
        {
            if (road == null)
                return;

            SplineContainer container = road.GetComponent<SplineContainer>();
            if (container == null)
                return;

            DrawSceneToolbar(container, road);
            DrawSplines(container);
            DrawInsertionButtons(container, road);
            DrawSocketSnapPreview(container, road);
        }

        public static void AddSpline(SplineContainer container, SplineRoad road)
        {
            if (container == null)
                return;

            Undo.RecordObject(container, "Add Road Spline");
            var splines = new List<Spline>(container.Splines);
            float spacing = road != null && road.Profile != null
                ? road.Profile.Width + 3f
                : 10f;
            float lateralOffset = splines.Count * spacing;

            var newSpline = new Spline();
            newSpline.Add(new BezierKnot(new float3(lateralOffset, 0f, -10f)));
            newSpline.Add(new BezierKnot(new float3(lateralOffset, 0f, 0f)));
            newSpline.Add(new BezierKnot(new float3(lateralOffset, 0f, 10f)));
            for (int i = 0; i < newSpline.Count; i++)
                newSpline.SetTangentMode(i, TangentMode.AutoSmooth);

            splines.Add(newSpline);
            container.Splines = splines;
            EditorUtility.SetDirty(container);
            road?.Rebuild();
            MarkSceneDirty(container);
            SplineSelection.Clear();
            SceneView.RepaintAll();
        }

        private static void DrawSceneToolbar(SplineContainer container, SplineRoad road)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 12f, 190f, 152f), GUI.skin.box);
            GUILayout.Label("ROAD SPLINE", EditorStyles.boldLabel);
            GUILayout.Label($"Splines: {container.Splines.Count}", EditorStyles.miniLabel);

            if (GUILayout.Button("+ Add Spline", GUILayout.Height(24f)))
                AddSpline(container, road);

            bool hasSelectedKnot = TryGetSelectedKnot(container, out _, out _, out Spline selectedSpline);
            bool canRemove = hasSelectedKnot && selectedSpline.Count > 2;
            using (new EditorGUI.DisabledScope(!hasSelectedKnot))
            {
                if (GUILayout.Button("+ Add Point at End", GUILayout.Height(22f)))
                    AddPointAtEnd(container, road);
            }

            using (new EditorGUI.DisabledScope(!canRemove))
            {
                if (GUILayout.Button("Remove Selected Point", GUILayout.Height(22f)))
                    RemoveSelectedPoint(container, road);
            }
            using (new EditorGUI.DisabledScope(!hasSelectedKnot))
            {
                if (GUILayout.Button(new GUIContent("Rotate Knot (E)", "Rotate the selected point and its curve handles."), GUILayout.Height(22f)))
                    RoadKnotRotationTool.Activate(road);
            }
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static void DrawSplines(SplineContainer container)
        {
            Event currentEvent = Event.current;
            bool repaint = currentEvent.type == EventType.Repaint;
            for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
            {
                Spline spline = container.Splines[splineIndex];
                if (spline == null || spline.Count == 0)
                    continue;

                if (repaint)
                {
                    var curvePoints = new Vector3[Mathf.Max(CurveStepsPerSegment, spline.Count * CurveStepsPerSegment) + 1];
                    for (int i = 0; i < curvePoints.Length; i++)
                    {
                        float t = i / (float)(curvePoints.Length - 1);
                        container.Evaluate(splineIndex, t, out float3 position, out _, out _);
                        curvePoints[i] = position;
                    }

                    Handles.color = new Color(0.2f, 0.85f, 1f, 0.9f);
                    Handles.DrawAAPolyLine(3f, curvePoints);

                    for (int knotIndex = 0; knotIndex < spline.Count; knotIndex++)
                    {
                        Vector3 knotPosition = container.transform.TransformPoint((Vector3)spline[knotIndex].Position);
                        float handleSize = HandleUtility.GetHandleSize(knotPosition);
                        Handles.color = new Color(1f, 0.68f, 0.2f, 1f);
                        Handles.SphereHandleCap(
                            GUIUtility.GetControlID(FocusType.Passive),
                            knotPosition,
                            Quaternion.identity,
                            handleSize * 0.085f,
                            EventType.Repaint);
                        Handles.Label(
                            knotPosition + Vector3.up * handleSize * 0.12f,
                            $"S{splineIndex + 1} · P{knotIndex + 1}",
                            EditorStyles.miniBoldLabel);
                    }
                }
            }

            Handles.color = Color.white;
        }

        private static void DrawInsertionButtons(SplineContainer container, SplineRoad road)
        {
            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.16f, 0.72f, 0.3f, 1f);
            Handles.BeginGUI();
            try
            {
                for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
                {
                    Spline spline = container.Splines[splineIndex];
                    if (spline == null || spline.Count < 2)
                        continue;

                    for (int segmentIndex = 0; segmentIndex < spline.Count - 1; segmentIndex++)
                    {
                        float t = (segmentIndex + 0.5f) / (spline.Count - 1f);
                        container.Evaluate(splineIndex, t, out float3 position, out _, out _);
                        Vector2 guiPosition = HandleUtility.WorldToGUIPoint(position);
                        var buttonRect = new Rect(guiPosition.x - 16f, guiPosition.y - 16f, 32f, 32f);
                        if (GUI.Button(buttonRect, new GUIContent("+", "Tambah titik di tengah segmen"), GetAddPointButtonStyle()))
                        {
                            InsertPoint(container, road, splineIndex, segmentIndex);
                            return;
                        }
                    }
                }
            }
            finally
            {
                Handles.EndGUI();
                GUI.backgroundColor = previousBackground;
            }
        }

        private static GUIStyle GetAddPointButtonStyle()
        {
            if (addPointButtonStyle != null)
                return addPointButtonStyle;

            addPointButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                fixedWidth = 32f,
                fixedHeight = 32f,
                padding = new RectOffset(0, 0, 0, 0)
            };
            addPointButtonStyle.normal.textColor = Color.white;
            addPointButtonStyle.hover.textColor = Color.white;
            addPointButtonStyle.active.textColor = Color.white;
            return addPointButtonStyle;
        }

        private static void DrawSocketSnapPreview(SplineContainer container, SplineRoad road)
        {
            RoadSocket[] sockets = Object.FindObjectsByType<RoadSocket>(FindObjectsSortMode.None);
            if (sockets.Length == 0)
                return;

            bool repaint = Event.current.type == EventType.Repaint;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            foreach (RoadSocket socket in sockets)
            {
                if (socket == null)
                    continue;

                float closestDistance = float.MaxValue;
                int closestSpline = -1;
                int closestKnot = -1;
                for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
                {
                    Spline spline = container.Splines[splineIndex];
                    if (spline == null || spline.Count < 2)
                        continue;

                    int[] endpoints = { 0, spline.Count - 1 };
                    foreach (int knotIndex in endpoints)
                    {
                        Vector3 position = container.transform.TransformPoint((Vector3)spline[knotIndex].Position);
                        float distance = Vector3.Distance(position, socket.transform.position);
                        if (distance >= closestDistance)
                            continue;

                        closestDistance = distance;
                        closestSpline = splineIndex;
                        closestKnot = knotIndex;
                    }
                }

                if (closestSpline < 0 || closestDistance > socket.SnapRadius)
                    continue;

                Spline nearestSpline = container.Splines[closestSpline];
                Vector3 endpoint = container.transform.TransformPoint((Vector3)nearestSpline[closestKnot].Position);
                if (repaint)
                {
                    Handles.color = socket.Occupied ? new Color(1f, 0.25f, 0.2f, 0.9f) : new Color(0.15f, 1f, 0.55f, 1f);
                    Handles.DrawWireDisc(socket.transform.position, socket.transform.up, socket.RoadWidth * 0.5f);
                    Handles.DrawAAPolyLine(3f, endpoint, socket.transform.position);
                    Handles.ArrowHandleCap(
                        GUIUtility.GetControlID(FocusType.Passive),
                        socket.transform.position,
                        socket.transform.rotation,
                        HandleUtility.GetHandleSize(socket.transform.position) * 0.7f,
                        EventType.Repaint);
                    Handles.Label(socket.transform.position + socket.transform.up * 0.4f, socket.Occupied ? "Connected" : "Click to Snap");
                }

                if (!socket.Occupied)
                {
                    float buttonSize = HandleUtility.GetHandleSize(socket.transform.position) * 0.18f;
                    if (Handles.Button(socket.transform.position, socket.transform.rotation, buttonSize, buttonSize * 2.2f, Handles.SphereHandleCap))
                    {
                        SnapEndpoint(container, road, closestSpline, closestKnot, socket);
                        return;
                    }
                }
            }

            Handles.color = Color.white;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        }

        private static void InsertPoint(SplineContainer container, SplineRoad road, int splineIndex, int leftKnotIndex)
        {
            Spline spline = container.Splines[splineIndex];
            float t = (leftKnotIndex + 0.5f) / (spline.Count - 1f);
            float3 position = SplineUtility.EvaluatePosition(spline, t);
            float3 tangent = SplineUtility.EvaluateTangent(spline, t);
            float3 tangentOffset = math.normalizesafe(tangent) * (math.length(tangent) * 0.25f);
            int insertIndex = leftKnotIndex + 1;

            Undo.RecordObject(container, "Add Spline Point");
            spline.Insert(insertIndex, new BezierKnot(position, -tangentOffset, tangentOffset, quaternion.identity));
            spline.SetTangentMode(insertIndex, TangentMode.Mirrored);
            EditorUtility.SetDirty(container);
            road.Rebuild();
            MarkSceneDirty(container);
            SplineSelection.Clear();
            SceneView.RepaintAll();
        }

        private static void AddPointAtEnd(SplineContainer container, SplineRoad road)
        {
            if (!TryGetSelectedKnot(container, out int splineIndex, out _, out _))
                return;

            Spline spline = container.Splines[splineIndex];
            int lastIndex = spline.Count - 1;
            BezierKnot endpoint = spline[lastIndex];
            float3 forward = math.mul(endpoint.Rotation, new float3(0f, 0f, 1f));
            float3 tangentOffset = forward * 2f;

            Undo.RecordObject(container, "Add Spline Point at End");
            spline.Add(new BezierKnot(endpoint.Position + forward * 5f, -tangentOffset, tangentOffset, endpoint.Rotation));
            spline.SetTangentMode(spline.Count - 1, TangentMode.Mirrored);
            EditorUtility.SetDirty(container);
            road.Rebuild();
            MarkSceneDirty(container);
            SplineSelection.Clear();
            SceneView.RepaintAll();
        }

        private static void RemoveSelectedPoint(SplineContainer container, SplineRoad road)
        {
            if (!TryGetSelectedKnot(container, out int splineIndex, out int knotIndex, out Spline spline) || spline.Count <= 2)
                return;

            Undo.RecordObject(container, "Remove Spline Point");
            spline.RemoveAt(knotIndex);
            EditorUtility.SetDirty(container);
            road.Rebuild();
            MarkSceneDirty(container);
            SplineSelection.Clear();
            SceneView.RepaintAll();
        }

        private static void SnapEndpoint(SplineContainer container, SplineRoad road, int splineIndex, int knotIndex, RoadSocket socket)
        {
            Spline spline = container.Splines[splineIndex];
            Undo.RecordObject(container, "Snap Road Endpoint");
            Undo.RecordObject(socket, "Connect Road Socket");

            BezierKnot knot = spline[knotIndex];
            knot.Position = container.transform.InverseTransformPoint(socket.transform.position);
            bool isFirstKnot = knotIndex == 0;
            Quaternion worldRotation = isFirstKnot
                ? Quaternion.LookRotation(-socket.transform.forward, socket.transform.up)
                : Quaternion.LookRotation(socket.transform.forward, socket.transform.up);
            Quaternion localRotation = Quaternion.Inverse(container.transform.rotation) * worldRotation;
            knot.Rotation = new quaternion(localRotation.x, localRotation.y, localRotation.z, localRotation.w);
            spline[knotIndex] = knot;
            spline.SetTangentMode(knotIndex, TangentMode.AutoSmooth);

            socket.SetOccupied(true);
            EditorUtility.SetDirty(container);
            EditorUtility.SetDirty(socket);
            road.Rebuild();
            MarkSceneDirty(container);
            SceneView.RepaintAll();
        }

        private static bool TryGetSelectedKnot(
            SplineContainer container,
            out int splineIndex,
            out int knotIndex,
            out Spline selectedSpline)
        {
            splineIndex = -1;
            knotIndex = -1;
            selectedSpline = null;
            if (container == null || SplineSelection.Count == 0)
                return false;

            var splineInfos = new List<SplineInfo>();
            for (int i = 0; i < container.Splines.Count; i++)
                splineInfos.Add(new SplineInfo(container, i));

            var knots = new List<SelectableKnot>();
            SplineSelection.GetElements(splineInfos, knots);
            foreach (SelectableKnot selectedKnot in knots)
            {
                if ((Object)selectedKnot.SplineInfo.Container != container)
                    continue;

                for (int i = 0; i < container.Splines.Count; i++)
                {
                    if (container.Splines[i] != selectedKnot.SplineInfo.Spline)
                        continue;

                    splineIndex = i;
                    knotIndex = selectedKnot.KnotIndex;
                    selectedSpline = container.Splines[i];
                    return true;
                }
            }

            return false;
        }

        private static void MarkSceneDirty(SplineContainer container)
        {
            if (container != null)
                EditorSceneManager.MarkSceneDirty(container.gameObject.scene);
        }
    }
}
