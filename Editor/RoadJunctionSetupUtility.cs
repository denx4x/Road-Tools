using UnityEditor;
using UnityEditor.SceneManagement;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadJunctionSetupUtility
    {
        internal static bool Refresh(SplineRoad road, out string message)
        {
            message="Select an editable road with linked junction knots.";
            if(road==null || road.IsBaked || road.Profile==null)return false;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Refresh Junction Cleanup");
            Ensure(road);
            RoadBuildReport result=road.Rebuild();
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
            RoadKnotConnectionUndoSync.Watch(road,null);
            message=result.Message;
            return result.Succeeded;
        }

        internal static void Ensure(SplineRoad road)
        {
            var settings=road.GetComponent<RoadJunctionSettings>();
            if(settings==null)settings=Undo.AddComponent<RoadJunctionSettings>(road.gameObject);
            if(settings.SurfaceMaterial!=null)return;
            Undo.RecordObject(settings,"Set Junction Surface");
            settings.SetSurfaceMaterial(RoadMaterialPresetCatalog.Load(5));
            EditorUtility.SetDirty(settings);
            PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
        }
    }
}
