using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit
{
    // Transient derived geometry. Native knot links remain the authored source of truth.
    internal sealed class RoadJunctionLayout
    {
        internal sealed class Footprint
        {
            internal int Spline;
            internal Vector2[] Polygon;
            internal Rect Bounds;
            internal float StartDistance,EndDistance;
            internal Footprint(int spline, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Spline = spline;
                Polygon = new[] { Xz(a), Xz(b), Xz(c), Xz(d) };
                float area = 0;
                for (int i = 0; i < 4; i++) area += Cross(Polygon[i], Polygon[(i + 1) % 4]);
                if (area < 0) System.Array.Reverse(Polygon);
                Vector2 low = Polygon[0], high = low;
                foreach (Vector2 point in Polygon) { low = Vector2.Min(low, point); high = Vector2.Max(high, point); }
                Bounds = Rect.MinMaxRect(low.x, low.y, high.x, high.y);
            }
        }

        internal readonly List<Footprint> Roads = new();
        internal readonly struct SurfaceSpan
        {
            internal readonly int Spline;
            internal readonly float StartV,EndV;
            internal SurfaceSpan(int spline,float start,float end) {Spline=spline;StartV=start;EndV=end;}
        }
        internal readonly List<SurfaceSpan> Surfaces = new();
        internal static Vector2 Xz(Vector3 point) => new(point.x, point.z);
        internal static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        internal static RoadJunctionLayout Build(SplineContainer container, RoadProfile profile)
        {
            var result = new RoadJunctionLayout();
            var seen = new HashSet<SplineKnotIndex>();
            var cache = new Dictionary<int, List<SplineSamplingUtility.Sample>>();
            var corners = new Dictionary<int, List<SplineSamplingUtility.Sample>>();
            var rowCache = new Dictionary<int, List<float>>();
            var footprintBranches = new HashSet<int>();
            for (int s = 0; s < container.Splines.Count; s++)
                for (int k = 0; k < container.Splines[s].Count; k++)
                {
                    var node = new SplineKnotIndex(s, k);
                    if (seen.Contains(node) || !container.KnotLinkCollection.TryGetKnotLinks(node, out var links) || links.Count < 2) continue;
                    var branches = new HashSet<int>();
                    foreach (var linked in links) { seen.Add(linked); branches.Add(linked.Spline); }
                    if (branches.Count < 2) continue;
                    Vector3 center = container.transform.TransformPoint((Vector3)container.Splines[s][k].Position);
                    foreach (int branch in branches)
                    {
                        if (!cache.TryGetValue(branch, out var samples))
                        {
                            samples = SplineSamplingUtility.BuildArcLengthSamples(container, branch, profile.SampleSpacing);
                            var corner = SplineSamplingUtility.BuildArcLengthSamples(container, branch, Mathf.Min(profile.SampleSpacing,.2f));
                            float[] cornerWidths = RoadSelfClearanceUtility.BuildMaximumWidths(corner,profile.Width);
                            bool dense = RoadCornerWidthUtility.HasTightCorner(corner,profile.Width) || RoadSelfClearanceUtility.HasWidthReduction(cornerWidths,profile.Width);
                            float spacing = dense ? Mathf.Min(profile.SampleSpacing,.2f) : profile.SampleSpacing;
                            if(dense)samples=corner;
                            cache.Add(branch,samples);corners.Add(branch,corner);
                            var sections=container.GetComponent<RoadMaterialSections>();
                            IReadOnlyList<RoadMaterialSpan> spans=sections!=null ? sections.Resolve(container,branch,samples) : System.Array.Empty<RoadMaterialSpan>();
                            var rows=new List<float>();float length=samples[samples.Count-1].Distance;
                            int chunks=Mathf.Max(1,Mathf.CeilToInt(length/Mathf.Max(1f,profile.ChunkLength)));
                            for(int chunk=0;chunk<chunks;chunk++)
                            {
                                var part=RoadMaterialMeshBuilder.RowDistances(chunk*profile.ChunkLength,Mathf.Min(length,(chunk+1)*profile.ChunkLength),spacing,spans);
                                if(rows.Count>0 && part.Count>0)part.RemoveAt(0);
                                rows.AddRange(part);
                            }
                            rowCache.Add(branch,rows);
                        }
                        var cornerSamples=corners[branch];
                        float[] widths = RoadSelfClearanceUtility.BuildMaximumWidths(cornerSamples, profile.Width);
                        float knotT=0;
                        foreach(var linked in links)if(linked.Spline==branch)
                        {knotT=container.Splines[branch].ConvertIndexUnit(linked.Knot,PathIndexUnit.Knot,PathIndexUnit.Normalized);break;}
                        float distance=0;
                        for(int i=0;i+1<samples.Count;i++)if(knotT>=samples[i].T && knotT<=samples[i+1].T)
                        {distance=Mathf.Lerp(samples[i].Distance,samples[i+1].Distance,Mathf.InverseLerp(samples[i].T,samples[i+1].T,knotT));break;}
                        float tile=Mathf.Max(.1f,profile.UvMetersPerTile);
                        float clearance=profile.Width;
                        float surfaceStart=Mathf.Max(0,Mathf.Floor((distance-clearance)/tile)*tile);
                        float surfaceEnd=Mathf.Min(samples[samples.Count-1].Distance,Mathf.Ceil((distance+clearance)/tile)*tile);
                        result.Surfaces.Add(new SurfaceSpan(branch,surfaceStart/tile,surfaceEnd/tile));
                        if(!footprintBranches.Add(branch))continue;
                        var rowDistances=rowCache[branch];
                        for (int i = 0; i + 1 < rowDistances.Count; i++)
                        {
                            var a = SplineSamplingUtility.EvaluateAtDistance(samples,rowDistances[i]);
                            var b = SplineSamplingUtility.EvaluateAtDistance(samples,rowDistances[i+1]);
                            Edge(cornerSamples, widths, profile.Width, a, out var al, out var ar);
                            Edge(cornerSamples, widths, profile.Width, b, out var bl, out var br);
                            result.Roads.Add(new Footprint(branch, al, ar, br, bl) {StartDistance=a.Distance,EndDistance=b.Distance});
                        }
                    }
                }
            // Shallow branches may overlap well beyond one road width. Include the
            // actual overlap extent before rounding marking boundaries to UV cycles.
            float uvTile=Mathf.Max(.01f,profile.UvMetersPerTile);
            foreach(var own in result.Roads)
                foreach(var other in result.Roads)
                    if(other.Spline!=own.Spline && own.Bounds.Overlaps(other.Bounds) && Intersects(own.Polygon,other.Polygon))
                    {
                        float length=cache[own.Spline][cache[own.Spline].Count-1].Distance;
                        result.Surfaces.Add(new SurfaceSpan(own.Spline,Mathf.Max(0,Mathf.Floor(own.StartDistance/uvTile)),Mathf.Min(length/uvTile,Mathf.Ceil(own.EndDistance/uvTile))));
                        break;
                    }
            result.Surfaces.Sort((a,b)=>a.Spline!=b.Spline ? a.Spline.CompareTo(b.Spline) : a.StartV.CompareTo(b.StartV));
            for(int i=result.Surfaces.Count-1;i>0;i--)
            {
                var previous=result.Surfaces[i-1];var current=result.Surfaces[i];
                if(previous.Spline==current.Spline && previous.EndV>=current.StartV)
                {result.Surfaces[i-1]=new SurfaceSpan(previous.Spline,previous.StartV,Mathf.Max(previous.EndV,current.EndV));result.Surfaces.RemoveAt(i);}
            }
            return result;
        }

        private static bool Intersects(Vector2[] a,Vector2[] b)
        {
            foreach(var polygon in new[]{a,b})for(int i=0;i<polygon.Length;i++)
            {
                Vector2 edge=polygon[(i+1)%polygon.Length]-polygon[i],axis=new Vector2(-edge.y,edge.x);
                float aMin=float.PositiveInfinity,aMax=float.NegativeInfinity,bMin=aMin,bMax=aMax;
                foreach(var p in a){float v=Vector2.Dot(p,axis);aMin=Mathf.Min(aMin,v);aMax=Mathf.Max(aMax,v);}
                foreach(var p in b){float v=Vector2.Dot(p,axis);bMin=Mathf.Min(bMin,v);bMax=Mathf.Max(bMax,v);}
                if(aMax<bMin || bMax<aMin)return false;
            }
            return true;
        }

        private static void Edge(List<SplineSamplingUtility.Sample> samples, float[] widths, float width,
            SplineSamplingUtility.Sample sample, out Vector3 left, out Vector3 right)
        {
            RoadCornerWidthUtility.EvaluateSideWidths(samples, sample.Distance, width, out float l, out float r);
            float max = RoadSelfClearanceUtility.EvaluateMaximumWidth(samples, widths, sample.Distance);
            if (l+r > max) { float scale=max/(l+r); l*=scale; r*=scale; }
            Vector3 across = Vector3.Cross(sample.Up,sample.Tangent).normalized;
            left=sample.Position-across*l; right=sample.Position+across*r;
        }

        internal SplinePropLayer WithOpenings(SplinePropLayer layer, int splineIndex,
            IReadOnlyList<SplineSamplingUtility.Sample> samples, RoadProfile profile, bool customPath = false)
        {
            if (Roads.Count == 0) return layer;
            SplinePropLayer copy = layer.Clone();
            float padding=RoadJunctionPropClearance.Padding(layer);
            foreach (float side in new[] { -1f, 0f, 1f })
            {
                var openings = new List<Vector2>();
                for (int i=0; i+1<samples.Count; i++)
                {
                    var a=samples[i]; var b=samples[i+1];
                    float lateral=side==0 ? layer.LateralOffset : side*(profile.Width*.5f+layer.LateralOffset);
                    Vector2 start=Xz(a.Position+Vector3.Cross(a.Up,a.Tangent).normalized*lateral);
                    Vector2 end=Xz(b.Position+Vector3.Cross(b.Up,b.Tangent).normalized*lateral);
                    foreach (Footprint mask in Roads)
                    {
                        if ((!customPath && mask.Spline==splineIndex) || !LineInterval(start,end,mask.Polygon,padding,out float low,out float high)) continue;
                        openings.Add(new Vector2(Mathf.Lerp(a.Distance,b.Distance,low),Mathf.Lerp(a.Distance,b.Distance,high)));
                    }
                }
                openings.Sort((a,b)=>a.x.CompareTo(b.x));
                for(int i=0;i<openings.Count;i++)
                {
                    Vector2 span=openings[i];
                    while(i+1<openings.Count && openings[i+1].x<=span.y+.001f)
                    {span.y=Mathf.Max(span.y,openings[++i].y);}
                    copy.AddDerivedGap(new PropPlacementGap(span.x,span.y,
                        side<0?PropSide.Left:side>0?PropSide.Right:PropSide.Center,splineIndex));
                }
            }
            return copy;
        }

        private static bool LineInterval(Vector2 a, Vector2 b, Vector2[] polygon, float padding, out float low, out float high)
        {
            low=0; high=1;
            for(int i=0;i<polygon.Length;i++)
            {
                Vector2 edge=polygon[(i+1)%polygon.Length]-polygon[i];
                float start=Cross(edge,a-polygon[i])+padding*edge.magnitude;
                float delta=Cross(edge,b-a);
                if(Mathf.Abs(delta)<.000001f) { if(start<0)return false; continue; }
                float crossing=-start/delta;
                if(delta>0)low=Mathf.Max(low,crossing);else high=Mathf.Min(high,crossing);
                if(high<=low)return false;
            }
            return high>low;
        }
    }
}
