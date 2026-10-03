using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadMaterialSectionScene
    {
        internal static void Draw(SplineRoad road, RoadMaterialSections sections, int index)
        {
            SplineContainer container = road.GetComponent<SplineContainer>();
            RoadMaterialSection section = sections.Sections[index];
            if (section == null || road.Profile == null || section.SplineIndex < 0 || section.SplineIndex >= container.Splines.Count) return;
            var samples = SplineSamplingUtility.BuildArcLengthSamples(container, section.SplineIndex, 0.5f);
            if (!RoadMaterialSections.TryGetRange(section, container.Splines[section.SplineIndex], samples, out float start, out float end)) return;
            float length = samples[samples.Count - 1].Distance;
            start = Mathf.Clamp(start, 0, length); end = Mathf.Clamp(end, 0, length);
            Color before = Handles.color;
            try
            {
                Handles.color = new Color(0.2f,0.85f,1f,0.18f);
                if (container.Splines[section.SplineIndex].Closed && end < start)
                { DrawSpan(road,samples,start,length); DrawSpan(road,samples,0,end); }
                else if (end > start) DrawSpan(road,samples,start,end);
                bool editable = !road.IsBaked && !EditorApplication.isPlayingOrWillChangePlaymode && section.RangeMode == RoadMaterialRangeMode.Distance;
                DrawBoundary(road, sections, index, samples, start, true, editable);
                DrawBoundary(road, sections, index, samples, end, false, editable);
            }
            finally { Handles.color = before; }
        }

        private static void DrawSpan(SplineRoad road, IReadOnlyList<SplineSamplingUtility.Sample> samples, float start, float end)
        {
            int count = Mathf.Clamp(Mathf.CeilToInt((end-start)/1f),1,300);
            Edges(road,SplineSamplingUtility.EvaluateAtDistance(samples,start),out Vector3 left,out Vector3 right);
            for (int i=1;i<=count;i++)
            {
                Edges(road,SplineSamplingUtility.EvaluateAtDistance(samples,Mathf.Lerp(start,end,i/(float)count)),out Vector3 nextLeft,out Vector3 nextRight);
                Handles.DrawAAConvexPolygon(left,nextLeft,nextRight,right);
                left=nextLeft;right=nextRight;
            }
        }

        private static void Edges(SplineRoad road,SplineSamplingUtility.Sample sample,out Vector3 left,out Vector3 right)
        {
            Vector3 side=Vector3.Cross(sample.Up,sample.Tangent).normalized*road.Profile.Width*0.5f;
            left=sample.Position-side;right=sample.Position+side;
            if(road.Profile.ConformRoadToTerrain)
            { left=RoadTerrainHeightUtility.Conform(left,road.Profile.TerrainSurfaceOffset);right=RoadTerrainHeightUtility.Conform(right,road.Profile.TerrainSurfaceOffset); }
            left+=Vector3.up*0.15f;right+=Vector3.up*0.15f;
        }

        private static void DrawBoundary(SplineRoad road, RoadMaterialSections sections, int index,
            IReadOnlyList<SplineSamplingUtility.Sample> samples, float distance, bool start, bool editable)
        {
            Edges(road,SplineSamplingUtility.EvaluateAtDistance(samples,distance),out Vector3 left,out Vector3 right);
            Vector3 center=(left+right)*0.5f;
            Handles.color = start ? new Color(0.25f,1f,0.7f) : new Color(1f,0.7f,0.25f);
            Handles.DrawAAPolyLine(3,left,right);
            Handles.Label(center+Vector3.up*0.5f,(start?"Start ":"End ")+distance.ToString("0.##")+" m");
            if(!editable) return;
            EditorGUI.BeginChangeCheck();
            Vector3 moved=Handles.FreeMoveHandle(center,HandleUtility.GetHandleSize(center)*0.09f,Vector3.zero,Handles.SphereHandleCap);
            if(!EditorGUI.EndChangeCheck()) return;
            float nearest=NearestDistance(samples,moved);
            var serialized=new SerializedObject(sections);
            serialized.FindProperty("sections").GetArrayElementAtIndex(index)
                .FindPropertyRelative(start?"startDistance":"endDistance").floatValue=nearest;
            RoadMaterialSectionEditing.Apply(sections,serialized,false);
        }

        internal static float NearestDistance(IReadOnlyList<SplineSamplingUtility.Sample> samples,Vector3 world)
        {
            float nearest=0,best=float.PositiveInfinity;
            for(int i=1;i<samples.Count;i++)
            {
                Vector3 a=samples[i-1].Position,b=samples[i].Position,line=b-a;
                float t=line.sqrMagnitude>0.000001f?Mathf.Clamp01(Vector3.Dot(world-a,line)/line.sqrMagnitude):0;
                float error=(world-Vector3.Lerp(a,b,t)).sqrMagnitude;
                if(error>=best)continue;best=error;nearest=Mathf.Lerp(samples[i-1].Distance,samples[i].Distance,t);
            }
            return nearest;
        }
    }
}
