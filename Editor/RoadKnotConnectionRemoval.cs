using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadKnotConnectionRemoval
    {
        internal static bool IsTracked(Spline spline) => spline.TryGetIntData(RoadKnotJunctionBridge.ConnectorTag,out var data)&&data.Count>0;

        internal static List<int> Candidates(SplineContainer container)
        {
            var result=new List<int>();
            if(container==null)return result;
            for(int i=0;i<container.Splines.Count;i++)
            {
                Spline spline=container.Splines[i];
                if(IsTracked(spline)) {result.Add(i);continue;}
                // Older connectors lack a marker. Offer only explicitly selected,
                // two-knot linked paths; never delete one automatically.
                if(spline.Closed || spline.Count!=2)continue;
                var first=new SplineKnotIndex(i,0);var last=new SplineKnotIndex(i,1);
                if(container.KnotLinkCollection.TryGetKnotLinks(first,out var a)&&a.Count>1 &&
                    container.KnotLinkCollection.TryGetKnotLinks(last,out var b)&&b.Count>1 &&
                    !container.AreKnotLinked(first,last))result.Add(i);
            }
            return result;
        }

        internal static bool Remove(SplineRoad road,int splineIndex,out string message)
        {
            message="Select an editable connector to remove.";
            if(road==null || road.IsBaked || !road.isActiveAndEnabled)return false;
            var container=road.GetComponent<SplineContainer>();
            if(container.Splines.Count<2 || !Candidates(container).Contains(splineIndex))return false;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Remove Road Connection");
            Undo.RecordObject(container,"Remove Road Connection");
            try
            {
                RemapSections(road.GetComponent<RoadMaterialSections>(),splineIndex);
                RemapProps(road.GetComponent<PropLayerManager>(),splineIndex);
                container.RemoveSplineAt(splineIndex);
                Undo.FlushUndoRecordObjects();
                EditorUtility.SetDirty(container);PrefabUtility.RecordPrefabInstancePropertyModifications(container);
                RoadBuildReport result=road.Rebuild();
                if(!result.Succeeded)throw new InvalidOperationException(result.Message);
                RoadKnotConnectionUndoSync.Watch(road,null);road.RequestRebuild();
                EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
                UnityEditor.Splines.SplineSelection.Clear();Selection.activeGameObject=road.gameObject;
                Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);SceneView.RepaintAll();
                message="Connector removed. Source branches remain in this road container; spline references were remapped. Undo restores the connection.";
                return true;
            }
            catch(Exception exception)
            {
                Undo.FlushUndoRecordObjects();Undo.RevertAllDownToGroup(group);road.RequestRebuild();
                message="Removal cancelled; source data restored. "+exception.Message;return false;
            }
        }

        private static void RemapSections(RoadMaterialSections component,int removed)
        {
            if(component==null)return;
            Undo.RegisterCompleteObjectUndo(component,"Remove Road Connection");
            var serialized=new SerializedObject(component);var sections=serialized.FindProperty("sections");
            for(int i=sections.arraySize-1;i>=0;i--)
            {
                var index=sections.GetArrayElementAtIndex(i).FindPropertyRelative("splineIndex");
                if(index.intValue==removed)sections.DeleteArrayElementAtIndex(i);
                else if(index.intValue>removed)index.intValue--;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        private static void RemapProps(PropLayerManager component,int removed)
        {
            if(component==null)return;
            Undo.RegisterCompleteObjectUndo(component,"Remove Road Connection");
            var serialized=new SerializedObject(component);var layers=serialized.FindProperty("layers");
            for(int i=0;i<layers.arraySize;i++)
            {
                if(component.Layers[i]==null || component.Layers[i].UsesCustomPath)continue;
                var layer=layers.GetArrayElementAtIndex(i);var index=layer.FindPropertyRelative("splineIndex");
                if(index.intValue==removed) {layer.FindPropertyRelative("enabled").boolValue=false;index.intValue=-1;}
                else if(index.intValue>removed)index.intValue--;
                var gaps=layer.FindPropertyRelative("gaps");
                for(int j=gaps.arraySize-1;j>=0;j--)
                {
                    var gap=gaps.GetArrayElementAtIndex(j).FindPropertyRelative("splineIndex");
                    if(gap.intValue==removed)gaps.DeleteArrayElementAtIndex(j);
                    else if(gap.intValue>removed)gap.intValue--;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }
}
