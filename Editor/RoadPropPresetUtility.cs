using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadPropPresetUtility
    {
        internal static bool ApplyBuiltin(SplineRoad road, RoadPropPresetKind kind, GameObject chosenFence = null)
        {
            if (!CanEdit(road, out PropLayerManager manager)) return false;
            bool needsFence = kind != RoadPropPresetKind.StreetLights;
            bool needsLights = kind != RoadPropPresetKind.Fence;
            GameObject fence = needsFence ? RoadToolsFenceAssetBuilder.GetOrCreatePrefab(chosenFence) : null;
            GameObject lamp = needsLights ? RoadPropPresetCatalog.FindStreetLightPrefab() : null;
            if ((needsFence && fence == null) || (needsLights && lamp == null))
            {
                Debug.LogWarning("Road Tools: a preset prefab is missing. Restore the Road Tools default assets before adding this preset.", road);
                return false;
            }

            Undo.RecordObject(manager, "Apply Road Prop Preset");
            var serialized = new SerializedObject(manager);
            SerializedProperty layers = serialized.FindProperty("layers");
            if (needsFence) ApplyDefaultLayer(layers, fence, true);
            if (needsLights) ApplyDefaultLayer(layers, lamp, false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Refresh(road, manager);
            return true;
        }

        internal static bool ApplyCustom(SplineRoad road, RoadPropPreset preset)
        {
            if (preset == null || preset.Layers.Count == 0 || !CanEdit(road, out PropLayerManager manager))
                return false;

            Undo.RecordObject(manager, "Apply Custom Road Prop Preset");
            var source = new SerializedObject(preset);
            var target = new SerializedObject(manager);
            SerializedProperty sourceLayers = source.FindProperty("layers");
            SerializedProperty targetLayers = target.FindProperty("layers");
            var matched = new HashSet<int>();
            for (int i = 0; i < sourceLayers.arraySize; i++)
            {
                SerializedProperty from = sourceLayers.GetArrayElementAtIndex(i);
                string name = from.FindPropertyRelative("name").stringValue;
                int index = FindNamedLayer(targetLayers, name, matched);
                if (index < 0)
                {
                    index = targetLayers.arraySize;
                    targetLayers.arraySize++;
                }
                matched.Add(index);
                CopyLayer(from, targetLayers.GetArrayElementAtIndex(index));
            }
            target.ApplyModifiedPropertiesWithoutUndo();
            Refresh(road, manager);
            return true;
        }

        internal static RoadPropPreset SaveCurrent(SplineRoad road)
        {
            if (road == null || !road.TryGetComponent(out PropLayerManager manager) || manager.Layers.Count == 0)
                return null;
            string folder = RoadToolsPackagePaths.GeneratedRoot + "/Presets";
            EnsureFolder(folder);
            string path = EditorUtility.SaveFilePanelInProject("Save Road Prop Preset", "Road Prop Preset", "asset",
                "Save the selected road's prop layers as a reusable preset.", folder);
            if (string.IsNullOrEmpty(path)) return null;

            var preset = ScriptableObject.CreateInstance<RoadPropPreset>();
            var source = new SerializedObject(manager);
            var target = new SerializedObject(preset);
            SerializedProperty sourceLayers = source.FindProperty("layers");
            SerializedProperty targetLayers = target.FindProperty("layers");
            targetLayers.arraySize = sourceLayers.arraySize;
            for (int i = 0; i < sourceLayers.arraySize; i++)
                CopyLayer(sourceLayers.GetArrayElementAtIndex(i), targetLayers.GetArrayElementAtIndex(i));
            target.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(preset);
            return preset;
        }

        internal static void Refresh(SplineRoad road, PropLayerManager manager)
        {
            EditorUtility.SetDirty(manager);
            road.RebuildProps();
            EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
            SceneView.RepaintAll();
        }

        private static bool CanEdit(SplineRoad road, out PropLayerManager manager)
        {
            manager = road != null ? road.GetComponent<PropLayerManager>() : null;
            return road != null && !road.IsBaked && road.Profile != null && manager != null;
        }

        private static void ApplyDefaultLayer(SerializedProperty layers, GameObject prefab, bool fence)
        {
            int index = FindDefaultLayer(layers, fence);
            bool added = index < 0;
            if (added)
            {
                index = layers.arraySize;
                layers.arraySize++;
            }
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            layer.FindPropertyRelative("name").stringValue = fence ? "Road Fence" : "Street Lamps";
            layer.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            layer.FindPropertyRelative("continuousFence").boolValue = fence;
            // Replacing a default model keeps the user's positioning and variation settings.
            if (!added) return;

            layer.FindPropertyRelative("side").enumValueIndex = (int)PropSide.Both;
            layer.FindPropertyRelative("grounding").enumValueIndex = (int)PropGroundingMode.Terrain;
            float fenceLength = prefab.TryGetComponent(out RoadFenceModel model) ? model.GetSegmentLength() : 3f;
            layer.FindPropertyRelative("spacing").floatValue = fence ? Mathf.Max(0.1f, fenceLength) : 9f;
            layer.FindPropertyRelative("startOffset").floatValue = fence ? 1.5f : 3f;
            layer.FindPropertyRelative("endOffset").floatValue = fence ? 1.5f : 3f;
            layer.FindPropertyRelative("lateralOffset").floatValue = fence ? 0.9f : 1.5f;
            layer.FindPropertyRelative("verticalOffset").floatValue = 0f;
            layer.FindPropertyRelative("followSplineRotation").boolValue = true;
            layer.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
            layer.FindPropertyRelative("randomRotation").vector3Value = Vector3.zero;
            layer.FindPropertyRelative("randomScale").vector2Value = Vector2.one;
            layer.FindPropertyRelative("seed").intValue = 12345;
            layer.FindPropertyRelative("intersectionExclusionDistance").floatValue = fence ? 1.5f : 3f;
        }

        private static int FindDefaultLayer(SerializedProperty layers, bool fence)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                string name = layer.FindPropertyRelative("name").stringValue;
                var prefab = layer.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                if (fence)
                {
                    if (name == "Road Fence" || name == "Roadside Fence" ||
                        (prefab != null && (prefab.name == "Road Fence" || prefab.name == "Roadside Fence"))) return i;
                }
                else if (name == "Street Lamps" || name == "Street Lights" ||
                    (prefab != null && prefab.name == "Street Lamp")) return i;
            }
            return -1;
        }

        private static int FindNamedLayer(SerializedProperty layers, string name, HashSet<int> matched)
        {
            for (int i = 0; i < layers.arraySize; i++)
                if (!matched.Contains(i) && layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
                    return i;
            return -1;
        }

        private static void CopyLayer(SerializedProperty source, SerializedProperty target)
        {
            target.FindPropertyRelative("name").stringValue = source.FindPropertyRelative("name").stringValue;
            target.FindPropertyRelative("prefab").objectReferenceValue = source.FindPropertyRelative("prefab").objectReferenceValue;
            target.FindPropertyRelative("side").enumValueIndex = source.FindPropertyRelative("side").enumValueIndex;
            target.FindPropertyRelative("grounding").enumValueIndex = source.FindPropertyRelative("grounding").enumValueIndex;
            foreach (string field in new[] { "spacing", "startOffset", "endOffset", "lateralOffset", "verticalOffset", "intersectionExclusionDistance" })
                target.FindPropertyRelative(field).floatValue = source.FindPropertyRelative(field).floatValue;
            foreach (string field in new[] { "followSplineRotation", "continuousFence" })
                target.FindPropertyRelative(field).boolValue = source.FindPropertyRelative(field).boolValue;
            foreach (string field in new[] { "rotationOffset", "randomRotation" })
                target.FindPropertyRelative(field).vector3Value = source.FindPropertyRelative(field).vector3Value;
            target.FindPropertyRelative("randomScale").vector2Value = source.FindPropertyRelative("randomScale").vector2Value;
            target.FindPropertyRelative("seed").intValue = source.FindPropertyRelative("seed").intValue;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string child = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(current, parts[i]);
                current = child;
            }
        }
    }
}
