using UnityEditor;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadToolsTerrainBandsPanel
    {
        internal static bool Draw(RoadProfile profile)
        {
            if (profile == null)
                return false;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("TERRAIN TEXTURE BANDS", RoadToolsWindowStyles.SectionTitle);
            EditorGUILayout.LabelField(
                "Distances start at the road edge. Zone order, textures and distances update registered terrain automatically.",
                RoadToolsWindowStyles.Body);

            var serialized = new SerializedObject(profile);
            SerializedProperty bands = serialized.FindProperty("terrainPaintBands");
            bool reordered = false;
            for (int i = 0; i < bands.arraySize; i++)
            {
                SerializedProperty band = bands.GetArrayElementAtIndex(i);
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Band {i + 1}", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(band.FindPropertyRelative("layer"), new GUIContent("Terrain Layer"));
                EditorGUILayout.PropertyField(band.FindPropertyRelative("startDistance"), new GUIContent("Start From Edge (m)"));
                EditorGUILayout.PropertyField(band.FindPropertyRelative("width"), new GUIContent("Width (m)"));
                EditorGUILayout.PropertyField(band.FindPropertyRelative("blendDistance"), new GUIContent("Blend (m)"));
                EditorGUILayout.BeginHorizontal();
                int moveTo = -1;
                if (GUILayout.Button("Move Up") && i > 0)
                    moveTo = i - 1;
                if (GUILayout.Button("Move Down") && i < bands.arraySize - 1)
                    moveTo = i + 1;
                if (GUILayout.Button("Remove"))
                {
                    bands.DeleteArrayElementAtIndex(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
                if (moveTo >= 0)
                {
                    MoveAndRelayout(bands, i, moveTo);
                    reordered = true;
                    break;
                }
            }

            if (GUILayout.Button("+ Add Texture Band", GUILayout.Height(27)))
            {
                int index = bands.arraySize;
                bands.InsertArrayElementAtIndex(index);
                SerializedProperty added = bands.GetArrayElementAtIndex(index);
                float start = 0f;
                if (index > 0)
                {
                    SerializedProperty previous = bands.GetArrayElementAtIndex(index - 1);
                    start = previous.FindPropertyRelative("startDistance").floatValue +
                            previous.FindPropertyRelative("width").floatValue;
                }
                added.FindPropertyRelative("layer").objectReferenceValue = null;
                added.FindPropertyRelative("startDistance").floatValue = start;
                added.FindPropertyRelative("width").floatValue = 3f;
                added.FindPropertyRelative("blendDistance").floatValue = 1f;
            }

            bool changed = serialized.ApplyModifiedProperties();
            if (changed)
            {
                EditorUtility.SetDirty(profile);
                RoadLiveUpdateCoordinator.QueueProfile(profile);
            }
            return changed || reordered;
        }

        internal static void MoveAndRelayout(SerializedProperty bands, int from, int to)
        {
            if (bands == null || from < 0 || to < 0 ||
                from >= bands.arraySize || to >= bands.arraySize || from == to)
                return;

            bands.MoveArrayElement(from, to);
            float start = 0f;
            for (int index = 0; index < bands.arraySize; index++)
            {
                SerializedProperty band = bands.GetArrayElementAtIndex(index);
                band.FindPropertyRelative("startDistance").floatValue = start;
                start += Mathf.Max(0.1f, band.FindPropertyRelative("width").floatValue);
            }
        }
    }
}
