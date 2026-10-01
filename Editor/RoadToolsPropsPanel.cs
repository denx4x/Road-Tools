using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadToolsPropsPanel
    {
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

            var serialized = new SerializedObject(manager);
            SerializedProperty layers = serialized.FindProperty("layers");
            EditorGUILayout.LabelField($"{layers.arraySize} layers on {road.name}", RoadToolsWindowStyles.Body);
            EditorGUILayout.Space(6);

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
            if (GUILayout.Button("+ Add Roadside Fence", GUILayout.Height(28)))
                AddFence(road, manager);
            if (GUILayout.Button("Rebuild Road Props"))
                Rebuild(road, manager);
        }

        private static void AddFence(SplineRoad road, PropLayerManager manager)
        {
            GameObject prefab = RoadToolsFenceAssetBuilder.GetOrCreatePrefab();
            if (prefab == null)
                return;

            var serialized = new SerializedObject(manager);
            SerializedProperty layers = serialized.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == "Roadside Fence")
                {
                    Debug.Log("This road already has a Roadside Fence layer.", road);
                    return;
                }
            }

            Undo.RecordObject(manager, "Add Roadside Fence");
            int index = layers.arraySize;
            layers.InsertArrayElementAtIndex(index);
            SerializedProperty fence = layers.GetArrayElementAtIndex(index);
            fence.FindPropertyRelative("name").stringValue = "Roadside Fence";
            fence.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            fence.FindPropertyRelative("side").enumValueIndex = (int)PropSide.Both;
            fence.FindPropertyRelative("grounding").enumValueIndex = (int)PropGroundingMode.Terrain;
            fence.FindPropertyRelative("spacing").floatValue = 3f;
            fence.FindPropertyRelative("startOffset").floatValue = 1.5f;
            fence.FindPropertyRelative("endOffset").floatValue = 1.5f;
            fence.FindPropertyRelative("intersectionExclusionDistance").floatValue = 1.5f;
            fence.FindPropertyRelative("lateralOffset").floatValue = 0.9f;
            fence.FindPropertyRelative("verticalOffset").floatValue = 0f;
            fence.FindPropertyRelative("followSplineRotation").boolValue = true;
            fence.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
            fence.FindPropertyRelative("randomRotation").vector3Value = Vector3.zero;
            fence.FindPropertyRelative("randomScale").vector2Value = Vector2.one;
            fence.FindPropertyRelative("continuousFence").boolValue = true;
            serialized.ApplyModifiedProperties();
            Rebuild(road, manager);
        }

        private static void Rebuild(SplineRoad road, PropLayerManager manager)
        {
            EditorUtility.SetDirty(manager);
            if (!road.IsBaked)
                road.Rebuild();
            EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
        }
    }
}
