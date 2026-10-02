using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    [Serializable]
    internal sealed class RoadKnotConnectionPanel
    {
        [SerializeField] private RoadKnotConnectionReference endpointA;
        [SerializeField] private RoadKnotConnectionReference endpointB;
        [SerializeField] private bool showPreview = true;
        [NonSerialized] private RoadKnotConnectionPlan cachedPlan;
        [NonSerialized] private RoadKnotConnectionReference lastObserved;

        internal void ObserveSelection()
        {
            var selected = RoadKnotConnectionUtility.SelectedKnots();
            if (selected.Count == 0) return;
            if (selected.Count == 2)
            {
                endpointA = selected[1]; endpointB = selected[0];
            }
            else if (selected.Count == 1)
            {
                RoadKnotConnectionReference current = selected[0];
                if (lastObserved != null && lastObserved.Matches(current)) return;
                if (endpointA == null) endpointA = current;
                else if (!endpointA.Matches(current)) endpointB = current;
                lastObserved = current;
            }
            cachedPlan = null;
            SceneView.RepaintAll();
        }

        internal void Draw(Action<string, bool> setStatus)
        {
            EditorGUILayout.HelpBox("Select endpoint A, then endpoint B in Scene View. Merge creates one continuous spline on road A; source points and existing curves are preserved. Internal knots require a junction instead.", MessageType.Info);
            DrawEndpoint("A", ref endpointA);
            DrawEndpoint("B", ref endpointB);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Swap A / B"))
            { var swap = endpointA; endpointA = endpointB; endpointB = swap; cachedPlan = null; }
            if (GUILayout.Button("Clear Selection"))
            { endpointA = endpointB = lastObserved = null; cachedPlan = null; }
            EditorGUILayout.EndHorizontal();
            showPreview = EditorGUILayout.Toggle("Show Connection Preview", showPreview);
            cachedPlan = RoadKnotConnectionGeometry.Plan(endpointA, endpointB);
            EditorGUILayout.HelpBox(cachedPlan.Message, cachedPlan.Succeeded && !cachedPlan.TightBend ? MessageType.Info : MessageType.Warning);
            using (new EditorGUI.DisabledScope(!cachedPlan.Succeeded || cachedPlan.TightBend))
                if (GUILayout.Button("Merge Roads — Sambungkan Spline", GUILayout.Height(32)))
                {
                    bool merged = RoadKnotConnectionUtility.Merge(endpointA, endpointB, out _, out string message);
                    setStatus(message, merged);
                    if (merged) { endpointA = endpointB = lastObserved = null; cachedPlan = null; }
                }
            if (GUI.changed) SceneView.RepaintAll();
        }

        private void DrawEndpoint(string name, ref RoadKnotConnectionReference endpoint)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Endpoint {name}", endpoint?.Label ?? "Not selected", EditorStyles.wordWrappedMiniLabel);
            var selected = RoadKnotConnectionUtility.SelectedKnots();
            using (new EditorGUI.DisabledScope(selected.Count != 1))
                if (GUILayout.Button("Capture " + name, GUILayout.Width(86)))
                { endpoint = selected[0]; cachedPlan = null; }
            EditorGUILayout.EndHorizontal();
        }

        internal void DrawPreview()
        {
            if (!showPreview || Event.current.type != EventType.Repaint) return;
            cachedPlan = RoadKnotConnectionGeometry.Plan(endpointA, endpointB);
            Color previous = Handles.color;
            var previousDepth = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            DrawMarker(endpointA, "A", new Color(0.1f, 0.8f, 1f));
            DrawMarker(endpointB, "B", new Color(1f, 0.65f, 0.1f));
            if (cachedPlan.Succeeded && cachedPlan.HasBridge)
            {
                Handles.color = cachedPlan.TightBend ? new Color(1f, 0.35f, 0.2f) : new Color(0.2f, 1f, 0.55f);
                var points = new Vector3[65];
                for (int i = 0; i < points.Length; i++) points[i] = CurveUtility.EvaluatePosition(cachedPlan.Bridge, i / 64f);
                Handles.DrawAAPolyLine(4f, points);
                Handles.DrawDottedLine(cachedPlan.Bridge.P0, cachedPlan.Bridge.P1, 4f);
                Handles.DrawDottedLine(cachedPlan.Bridge.P2, cachedPlan.Bridge.P3, 4f);
            }
            Handles.color = previous;
            Handles.zTest = previousDepth;
        }

        private static void DrawMarker(RoadKnotConnectionReference endpoint, string label, Color color)
        {
            if (endpoint == null || !endpoint.TryResolve(out _, out _, out _)) return;
            Vector3 point = endpoint.Position;
            float size = HandleUtility.GetHandleSize(point);
            Handles.color = color;
            Handles.DrawWireDisc(point, Vector3.up, size * 0.16f);
            Handles.Label(point + Vector3.up * size * 0.2f, "Merge " + label, EditorStyles.boldLabel);
        }
    }
}
