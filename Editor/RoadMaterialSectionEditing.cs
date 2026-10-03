using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    [InitializeOnLoad]
    internal static class RoadMaterialSectionEditing
    {
        internal const string UndoName = "Edit Road Material Section";
        static RoadMaterialSectionEditing() => Undo.undoRedoEvent += OnUndoRedo;

        internal static RoadMaterialSections Add(SplineRoad road, RoadMaterialSection section)
        {
            if (road == null || road.IsBaked || EditorApplication.isPlayingOrWillChangePlaymode) return null;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(UndoName);
            RoadMaterialSections sections = road.GetComponent<RoadMaterialSections>();
            // This is automatic support infrastructure. Record its authored list,
            // rather than a component-add hierarchy snapshot containing transient meshes.
            if (sections == null) sections = road.gameObject.AddComponent<RoadMaterialSections>();
            Undo.RegisterCompleteObjectUndo(sections, UndoName);
            sections.Add(section); Refresh(sections);
            Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group);
            return sections;
        }

        internal static void Apply(RoadMaterialSections sections, SerializedObject serialized, bool newGroup = true)
        {
            if (newGroup) Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            Undo.RegisterCompleteObjectUndo(sections, UndoName);
            serialized.ApplyModifiedPropertiesWithoutUndo(); Refresh(sections);
            Undo.FlushUndoRecordObjects();
        }

        internal static void Remove(RoadMaterialSections sections, int index)
        {
            if (sections == null || sections.GetComponent<SplineRoad>().IsBaked) return;
            Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName(UndoName);
            Undo.RegisterCompleteObjectUndo(sections, UndoName);
            sections.RemoveAt(index); Refresh(sections); Undo.FlushUndoRecordObjects();
        }

        private static void Refresh(RoadMaterialSections sections)
        {
            sections.Refresh(); EditorUtility.SetDirty(sections);
            PrefabUtility.RecordPrefabInstancePropertyModifications(sections);
            if (sections.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(sections.gameObject.scene);
            SceneView.RepaintAll();
        }

        private static void OnUndoRedo(in UndoRedoInfo info)
        {
            if (info.undoName != UndoName && info.undoName != RoadMaterialUtility.MaterialUndoName) return;
            foreach (RoadMaterialSections sections in Resources.FindObjectsOfTypeAll<RoadMaterialSections>())
                if (!EditorUtility.IsPersistent(sections) && sections.gameObject.scene.IsValid() && sections.gameObject.scene.isLoaded) sections.Refresh();
            SceneView.RepaintAll();
        }
    }
}
