using UnityEditor;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadToolsPropsPanel
    {
        private static GameObject fencePrefab;
        private static RoadPropPreset customPreset;
        private static bool showAdvanced;
        private static GUIStyle presetButton;

        internal static void Draw(SplineRoad road)
        {
            if (road == null)
            {
                EditorGUILayout.LabelField("Select a road to edit its prop layers.", RoadToolsWindowStyles.Body);
                return;
            }

            PropLayerManager manager = road.GetComponent<PropLayerManager>();
            if (manager == null)
            {
                EditorGUILayout.LabelField("This road has no Prop Layer Manager.", RoadToolsWindowStyles.Body);
                return;
            }

            EditorGUILayout.LabelField($"{manager.Layers.Count} layers on {road.name}", RoadToolsWindowStyles.Body);
            EditorGUILayout.Space(6);

            if (road.IsBaked)
                EditorGUILayout.HelpBox("This road is baked. Use Resume Editing in the Road tab to change its props.", MessageType.Info);
            else if (road.Profile == null)
                EditorGUILayout.HelpBox("Assign a Road Profile in the Road tab before adding props.", MessageType.Info);

            using (new EditorGUI.DisabledScope(road.IsBaked || road.Profile == null))
            {
                RoadToolsPropSectionsPanel.DrawGeneration(road, manager);
                EditorGUILayout.Space(8);
                DrawQuickAdd(road, manager);
                EditorGUILayout.Space(10);
                DrawCustomPreset(road);
                EditorGUILayout.Space(10);
                RoadToolsPropSectionsPanel.Draw(road, manager);
                EditorGUILayout.Space(10);
                showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Prop Layers", true);
                if (showAdvanced) DrawAdvanced(road, manager);
            }
        }

        private static void DrawQuickAdd(SplineRoad road, PropLayerManager manager)
        {
            EditorGUILayout.LabelField("QUICK ADD", RoadToolsWindowStyles.SectionTitle);
            EditorGUILayout.LabelField("Ready to use presets. Clicking again updates the existing layer.", RoadToolsWindowStyles.Body);
            EditorGUILayout.Space(5);

            GameObject preferredFence = fencePrefab != null ? fencePrefab : RoadToolsFenceAssetBuilder.FindPreferredPrefab();
            GameObject lamp = RoadPropPresetCatalog.FindStreetLightPrefab();
            bool twoColumns = EditorGUIUtility.currentViewWidth > 560f;
            if (twoColumns) EditorGUILayout.BeginHorizontal();
            if (DrawPresetCard(RoadPropPresetKind.Fence, preferredFence)) AddFence(road, manager);
            if (DrawPresetCard(RoadPropPresetKind.StreetLights, lamp))
                RoadPropPresetUtility.ApplyBuiltin(road, RoadPropPresetKind.StreetLights);
            if (twoColumns) EditorGUILayout.EndHorizontal();
            if (DrawPresetCard(RoadPropPresetKind.FenceAndLights, preferredFence))
                RoadPropPresetUtility.ApplyBuiltin(road, RoadPropPresetKind.FenceAndLights, fencePrefab);
        }

        private static bool DrawPresetCard(RoadPropPresetKind kind, GameObject prefab)
        {
            if (presetButton == null)
                presetButton = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    imagePosition = ImagePosition.ImageLeft,
                    wordWrap = true,
                    padding = new RectOffset(8, 8, 6, 6)
                };
            string label = RoadPropPresetCatalog.Label(kind);
            string description = RoadPropPresetCatalog.Description(kind);
            Vector2 previousIconSize = EditorGUIUtility.GetIconSize();
            EditorGUIUtility.SetIconSize(new Vector2(50, 50));
            bool clicked = GUILayout.Button(new GUIContent(label + "\n" + description,
                RoadPropPresetCatalog.Preview(prefab), description), presetButton, GUILayout.Height(68));
            EditorGUIUtility.SetIconSize(previousIconSize);
            return clicked;
        }

        private static void DrawCustomPreset(SplineRoad road)
        {
            EditorGUILayout.LabelField("YOUR PRESETS", RoadToolsWindowStyles.SectionTitle);
            customPreset = (RoadPropPreset)EditorGUILayout.ObjectField("Prop Preset", customPreset, typeof(RoadPropPreset), false);
            if (customPreset != null && !string.IsNullOrWhiteSpace(customPreset.Description))
                EditorGUILayout.LabelField(customPreset.Description, RoadToolsWindowStyles.Body);
            if (customPreset != null && customPreset.Thumbnail != null)
                GUILayout.Label(customPreset.Thumbnail, GUILayout.Width(70), GUILayout.Height(50));
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(customPreset == null || customPreset.Layers.Count == 0))
                if (GUILayout.Button("Apply Preset", GUILayout.Height(26))) RoadPropPresetUtility.ApplyCustom(road, customPreset);
            using (new EditorGUI.DisabledScope(road.GetComponent<PropLayerManager>().Layers.Count == 0))
                if (GUILayout.Button("Save Current as Preset", GUILayout.Height(26)))
                {
                    RoadPropPreset saved = RoadPropPresetUtility.SaveCurrent(road);
                    if (saved != null) customPreset = saved;
                }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawAdvanced(SplineRoad road, PropLayerManager manager)
        {
            var serialized = new SerializedObject(manager);
            SerializedProperty layers = serialized.FindProperty("layers");
            for (int index = 0; index < layers.arraySize; index++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                string label = layer.FindPropertyRelative("name").stringValue;
                EditorGUILayout.PropertyField(layer,
                    new GUIContent(string.IsNullOrWhiteSpace(label) ? $"Prop Layer {index + 1}" : label), true);
                if (!layer.isExpanded) continue;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Move Up") && index > 0)
                    layers.MoveArrayElement(index, index - 1);
                if (GUILayout.Button("Move Down") && index < layers.arraySize - 1)
                    layers.MoveArrayElement(index, index + 1);
                if (GUILayout.Button("Remove"))
                {
                    layers.DeleteArrayElementAtIndex(index);
                    index--;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5);
            }

            if (serialized.ApplyModifiedProperties())
                Rebuild(road, manager);

            if (GUILayout.Button("+ Add Prop Layer", GUILayout.Height(28)))
            {
                Undo.RecordObject(manager, "Add Road Prop Layer");
                manager.AddLayer();
                Rebuild(road, manager);
            }
            EditorGUILayout.Space(6);
            EditorGUI.BeginChangeCheck();
            GameObject chosenFence = (GameObject)EditorGUILayout.ObjectField("Fence Prefab",
                fencePrefab != null ? fencePrefab : RoadToolsFenceAssetBuilder.FindPreferredPrefab(), typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) fencePrefab = chosenFence;
            EditorGUILayout.LabelField("Imported fence models bend along the road. Existing fence layers are updated in place.", RoadToolsWindowStyles.Body);
            if (GUILayout.Button("Add / Replace Road Fence", GUILayout.Height(28)))
                AddFence(road, manager);
            if (GUILayout.Button("Rebuild Road Props"))
                Rebuild(road, manager);
        }

        private static void AddFence(SplineRoad road, PropLayerManager manager)
        {
            RoadPropPresetUtility.ApplyBuiltin(road, RoadPropPresetKind.Fence, fencePrefab);
        }

        private static void Rebuild(SplineRoad road, PropLayerManager manager)
        {
            RoadPropPresetUtility.Refresh(road, manager);
        }
    }
}
