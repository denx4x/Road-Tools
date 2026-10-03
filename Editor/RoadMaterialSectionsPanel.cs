using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    [Serializable]
    internal sealed class RoadMaterialSectionsPanel
    {
        [SerializeField] private int selectedSection;
        [SerializeField] private bool showPreview = true;

        internal void Draw(SplineRoad road)
        {
            if (road == null) return;
            RoadMaterialSections sections = road.GetComponent<RoadMaterialSections>();
            SplineContainer container = road.GetComponent<SplineContainer>();
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("LOCAL MATERIALS", RoadToolsWindowStyles.SectionTitle);
            EditorGUILayout.LabelField("Override only a section of this road. Other parts keep the main material.", RoadToolsWindowStyles.Body);
            using (new EditorGUI.DisabledScope(road.IsBaked || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ By Distance", GUILayout.Height(26)))
                {
                    sections = RoadMaterialSectionEditing.Add(road, new RoadMaterialSection(road.EffectiveMaterial, 0, 0, 10));
                    if (sections != null) selectedSection = sections.Sections.Count - 1;
                }
                bool selected = TryKnotRange(container, out int splineIndex, out int startKnot, out int endKnot);
                using (new EditorGUI.DisabledScope(!selected))
                    if (GUILayout.Button("+ From Knots", GUILayout.Height(26)))
                    {
                        sections = RoadMaterialSectionEditing.Add(road, RoadMaterialSection.BetweenKnots(road.EffectiveMaterial, splineIndex, startKnot, endKnot));
                        if (sections != null) selectedSection = sections.Sections.Count - 1;
                    }
                EditorGUILayout.EndHorizontal();
            }
            if (sections == null || sections.Sections.Count == 0)
            {
                EditorGUILayout.LabelField("Select two knots on one spline, or one knot to use its next segment.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            var names = new string[sections.Sections.Count];
            for (int i = 0; i < names.Length; i++) names[i] = "Section " + (i + 1) + " · " +
                (sections.Sections[i]?.Material != null ? sections.Sections[i].Material.name : "Main road material");
            selectedSection = EditorGUILayout.Popup("Edit Section", Mathf.Clamp(selectedSection, 0, names.Length - 1), names);
            var serialized = new SerializedObject(sections);
            SerializedProperty entry = serialized.FindProperty("sections").GetArrayElementAtIndex(selectedSection);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(road.IsBaked || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("enabled"), new GUIContent("Enable Section"));
                var splineNames = new string[container.Splines.Count];
                for (int i = 0; i < splineNames.Length; i++) splineNames[i] = "Spline " + (i + 1);
                SerializedProperty spline = entry.FindPropertyRelative("splineIndex");
                spline.intValue = EditorGUILayout.Popup("Road Spline", spline.intValue, splineNames);
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("rangeMode"), new GUIContent("Range"));
                bool knots = entry.FindPropertyRelative("rangeMode").enumValueIndex == (int)RoadMaterialRangeMode.Knots;
                if (knots)
                {
                    var start = entry.FindPropertyRelative("startKnot"); var end = entry.FindPropertyRelative("endKnot");
                    start.intValue = Mathf.Max(0, EditorGUILayout.IntField("From Point", start.intValue + 1) - 1);
                    end.intValue = Mathf.Max(0, EditorGUILayout.IntField("To Point", end.intValue + 1) - 1);
                }
                else
                {
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("startDistance"), new GUIContent("From (m)"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("endDistance"), new GUIContent("To (m)"));
                }
            }
            Material material = (Material)entry.FindPropertyRelative("material").objectReferenceValue;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                RoadToolsMaterialPanel.DrawChoice(ref material, road.Profile, false, "Main Road Material");
            entry.FindPropertyRelative("material").objectReferenceValue = material;
            if (EditorGUI.EndChangeCheck()) RoadMaterialSectionEditing.Apply(sections, serialized);
            showPreview = EditorGUILayout.Toggle("Show Section in Scene", showPreview);
            RoadMaterialSection section = sections.Sections[selectedSection];
            if (section.SplineIndex >= 0 && section.SplineIndex < container.Splines.Count)
            {
                var samples = SplineSamplingUtility.BuildArcLengthSamples(container, section.SplineIndex, 0.5f);
                if (RoadMaterialSections.TryGetRange(section, container.Splines[section.SplineIndex], samples, out float from, out float to))
                {
                    float length = samples[samples.Count-1].Distance;
                    from = Mathf.Clamp(from, 0, length); to = Mathf.Clamp(to, 0, length);
                    EditorGUILayout.LabelField($"Section: {from:0.##} → {to:0.##} m / {length:0.##} m", EditorStyles.wordWrappedMiniLabel);
                    if (Mathf.Abs(to-from) < 0.00001f || (to < from && !container.Splines[section.SplineIndex].Closed))
                        EditorGUILayout.HelpBox("Choose a non-empty range. To must be after From on an open spline.", MessageType.Warning);
                }
                else EditorGUILayout.HelpBox("Point numbers or distances are invalid for this spline.", MessageType.Warning);
            }
            else EditorGUILayout.HelpBox("The referenced road spline is missing. Choose an available spline.", MessageType.Warning);
            if (road.IsBaked) EditorGUILayout.LabelField("Resume editing to change ranges. Section materials can still be changed.", RoadToolsWindowStyles.Body);
            else using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Remove Section")) { RoadMaterialSectionEditing.Remove(sections, selectedSection); selectedSection = Mathf.Max(0, selectedSection - 1); }
            EditorGUILayout.LabelField("Overlapping sections: the later section wins. Distance boundaries can be dragged in Scene View.", EditorStyles.wordWrappedMiniLabel);
            if (GUI.changed) SceneView.RepaintAll();
        }

        internal void DrawScene(SplineRoad road)
        {
            if (!showPreview || road == null) return;
            var sections = road.GetComponent<RoadMaterialSections>();
            if (sections == null || selectedSection < 0 || selectedSection >= sections.Sections.Count) return;
            RoadMaterialSectionScene.Draw(road, sections, selectedSection);
        }

        internal static bool TryKnotRange(SplineContainer container, out int splineIndex, out int first, out int last)
        {
            splineIndex = first = last = -1;
            if (container == null) return false;
            var infos = new List<SplineInfo>(); var knots = new List<SelectableKnot>();
            for(int i=0;i<container.Splines.Count;i++) infos.Add(new SplineInfo(container,i));
            SplineSelection.GetElements(infos,knots);
            foreach (SelectableKnot knot in knots)
            {
                int index = knot.SplineInfo.Index;
                if (splineIndex >= 0 && splineIndex != index) return false;
                splineIndex = index; first = first < 0 ? knot.KnotIndex : Mathf.Min(first,knot.KnotIndex);
                last = Mathf.Max(last,knot.KnotIndex);
            }
            if (splineIndex < 0) return false;
            if (first == last)
            {
                last++;
                if (last >= container.Splines[splineIndex].Count)
                { if (!container.Splines[splineIndex].Closed) return false; last = 0; }
            }
            return true;
        }
    }
}
