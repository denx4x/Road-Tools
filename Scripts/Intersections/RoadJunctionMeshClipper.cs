using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dyma.SplineLevelToolkit
{
    // Convex polygon subtraction preserves the source triangle's winding and UVs.
    internal static class RoadJunctionMeshClipper
    {
        private struct Vertex
        {
            internal Vector3 Position;
            internal Vector3 Normal;
            internal Vector2 Uv;
            internal static Vertex Lerp(Vertex a, Vertex b, float t) => new()
            { Position=Vector3.LerpUnclamped(a.Position,b.Position,t), Normal=Vector3.LerpUnclamped(a.Normal,b.Normal,t).normalized, Uv=Vector2.LerpUnclamped(a.Uv,b.Uv,t) };
        }

        internal static int[] Apply(Mesh mesh, Transform owner, int splineIndex,
            RoadJunctionLayout junctions, int[] slots, bool useSurface)
        {
            if(junctions.Roads.Count==0)return slots;
            Vector3[] positions=mesh.vertices; Vector2[] uvs=mesh.uv;
            Vector3[] normals=mesh.normals;
            if(positions.Length==0)return slots;
            Vector2 low=RoadJunctionLayout.Xz(owner.TransformPoint(positions[0])),high=low;
            foreach(Vector3 vertex in positions)
            {Vector2 point=RoadJunctionLayout.Xz(owner.TransformPoint(vertex));low=Vector2.Min(low,point);high=Vector2.Max(high,point);}
            Rect bounds=Rect.MinMaxRect(low.x,low.y,high.x,high.y);
            var roadMasks=new List<RoadJunctionLayout.Footprint>();
            var surfaceMasks=new List<RoadJunctionLayout.SurfaceSpan>();
            float lowV=float.PositiveInfinity,highV=float.NegativeInfinity;
            foreach(Vector2 uv in uvs) {lowV=Mathf.Min(lowV,uv.y);highV=Mathf.Max(highV,uv.y);}
            foreach(var mask in junctions.Roads)
                if(mask.Spline!=splineIndex && bounds.Overlaps(mask.Bounds))roadMasks.Add(mask);
            if(useSurface)foreach(var mask in junctions.Surfaces)
                if(mask.Spline==splineIndex && highV>=mask.StartV && lowV<=mask.EndV)surfaceMasks.Add(mask);
            if(roadMasks.Count==0&&surfaceMasks.Count==0)return slots;
            int originalCount=mesh.subMeshCount;
            var vertices=new List<Vector3>(); var outputUvs=new List<Vector2>();
            var outputNormals=new List<Vector3>();
            var triangles=new List<int>[originalCount+(useSurface?1:0)];
            for(int i=0;i<triangles.Length;i++)triangles[i]=new List<int>();
            for(int sub=0;sub<originalCount;sub++)
            {
                int[] indices=mesh.GetTriangles(sub);
                for(int i=0;i<indices.Length;i+=3)
                {
                    var polygon=new List<Vertex>(3);
                    for(int j=0;j<3;j++) { int index=indices[i+j]; polygon.Add(new Vertex {Position=owner.TransformPoint(positions[index]),Normal=normals[index],Uv=uvs[index]}); }
                    var pieces=new List<List<Vertex>> {polygon};
                    bool wall=Mathf.Abs(Vector3.Cross(polygon[1].Position-polygon[0].Position,
                        polygon[2].Position-polygon[0].Position).normalized.y)<.2f;
                    foreach(var mask in roadMasks)
                    {
                        if(mask.Spline==splineIndex || (!wall && mask.Spline>=splineIndex))continue;
                        pieces=Subtract(pieces,mask,null);
                        if(pieces.Count==0)break;
                    }
                    if(useSurface)
                        foreach(var mask in surfaceMasks)
                        {
                            var marked=new List<List<Vertex>>();
                            var outside=new List<List<Vertex>>();
                            foreach(var piece in pieces)
                            {
                                var before=ClipUv(piece,mask.StartV,false);
                                if(before.Count>=3)outside.Add(before);
                                var inside=ClipUv(piece,mask.StartV,true);
                                var after=ClipUv(inside,mask.EndV,true);
                                if(after.Count>=3)outside.Add(after);
                                inside=ClipUv(inside,mask.EndV,false);
                                if(inside.Count>=3)marked.Add(inside);
                            }
                            pieces=outside;
                            foreach(var area in marked)Emit(area,triangles[originalCount],owner,vertices,outputUvs,outputNormals);
                        }
                    foreach(var area in pieces)Emit(area,triangles[sub],owner,vertices,outputUvs,outputNormals);
                }
            }
            mesh.Clear(); mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(vertices); mesh.SetUVs(0,outputUvs); mesh.SetNormals(outputNormals); mesh.subMeshCount=triangles.Length;
            for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i);
            var result=new int[triangles.Length];
            for(int i=0;i<originalCount;i++)result[i]=slots!=null?slots[i]:-1;
            if(useSurface)result[originalCount]=-2;
            return result;
        }

        private static List<List<Vertex>> Subtract(List<List<Vertex>> polygons,
            RoadJunctionLayout.Footprint mask, List<List<Vertex>> insideOutput)
        {
            var outside=new List<List<Vertex>>();
            foreach(var polygon in polygons)
            {
                if(!Overlaps(polygon,mask.Bounds)) {outside.Add(polygon);continue;}
                List<Vertex> inside=polygon;
                for(int edge=0;edge<mask.Polygon.Length && inside.Count>=3;edge++)
                {
                    Vector2 a=mask.Polygon[edge],b=mask.Polygon[(edge+1)%mask.Polygon.Length];
                    List<Vertex> removed=Clip(inside,a,b,false);
                    if(removed.Count>=3)outside.Add(removed);
                    inside=Clip(inside,a,b,true);
                }
                if(inside.Count>=3)insideOutput?.Add(inside);
            }
            return outside;
        }

        private static bool Overlaps(List<Vertex> polygon, Rect bounds)
        {
            Vector2 low=RoadJunctionLayout.Xz(polygon[0].Position),high=low;
            foreach(var vertex in polygon) {Vector2 p=RoadJunctionLayout.Xz(vertex.Position);low=Vector2.Min(low,p);high=Vector2.Max(high,p);}
            return high.x>=bounds.xMin && low.x<=bounds.xMax && high.y>=bounds.yMin && low.y<=bounds.yMax;
        }

        private static List<Vertex> Clip(List<Vertex> input, Vector2 a, Vector2 b, bool inside)
        {
            var result=new List<Vertex>();
            if(input.Count==0)return result;
            Vertex previous=input[input.Count-1];
            float before=RoadJunctionLayout.Cross(b-a,RoadJunctionLayout.Xz(previous.Position)-a);
            foreach(Vertex current in input)
            {
                float after=RoadJunctionLayout.Cross(b-a,RoadJunctionLayout.Xz(current.Position)-a);
                bool prevInside=inside?before>=0:before<=0,curInside=inside?after>=0:after<=0;
                if(prevInside!=curInside)result.Add(Vertex.Lerp(previous,current,before/(before-after)));
                if(curInside)result.Add(current);
                previous=current;before=after;
            }
            return result;
        }

        private static List<Vertex> ClipUv(List<Vertex> input,float boundary,bool greater)
        {
            var result=new List<Vertex>();if(input.Count==0)return result;
            Vertex previous=input[input.Count-1];float before=previous.Uv.y-boundary;
            foreach(Vertex current in input)
            {
                float after=current.Uv.y-boundary;
                bool a=greater?before>=0:before<=0,b=greater?after>=0:after<=0;
                if(a!=b)result.Add(Vertex.Lerp(previous,current,before/(before-after)));
                if(b)result.Add(current);previous=current;before=after;
            }
            return result;
        }

        private static void Emit(List<Vertex> polygon,List<int> triangles,Transform owner,List<Vector3> positions,List<Vector2> uvs,List<Vector3> normals)
        {
            for(int i=1;i+1<polygon.Count;i++)
            {
                if(Vector3.Cross(polygon[i].Position-polygon[0].Position,polygon[i+1].Position-polygon[0].Position).sqrMagnitude<.0000000001f)continue;
                foreach(Vertex vertex in new[] {polygon[0],polygon[i],polygon[i+1]})
                {triangles.Add(positions.Count);positions.Add(owner.InverseTransformPoint(vertex.Position));uvs.Add(vertex.Uv);normals.Add(vertex.Normal);}
            }
        }
    }
}
