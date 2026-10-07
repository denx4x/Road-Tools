using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    // A distant pair needs an edge between two junctions, not a displaced source node.
    internal static class RoadKnotJunctionBridge
    {
        internal const float WeldTolerance = 0.01f;
        internal const string ConnectorTag = "RoadTools.JunctionConnector";

        internal static bool NeedsBridge(Vector3 a, Vector3 b) =>
            Vector3.Distance(a, b) > WeldTolerance;

        internal static Spline Create(SplineContainer container, Vector3 a, Vector3 b)
        {
            Vector3 start = container.transform.InverseTransformPoint(a);
            Vector3 end = container.transform.InverseTransformPoint(b);
            Vector3 handle = (end - start) / 3f;
            var spline = new Spline();
            spline.Add(new BezierKnot((float3)start, (float3)(-handle), (float3)handle, quaternion.identity), TangentMode.Broken);
            spline.Add(new BezierKnot((float3)end, (float3)(-handle), (float3)handle, quaternion.identity), TangentMode.Broken);
            spline.GetOrCreateIntData(ConnectorTag).Add(0,1);
            return spline;
        }
    }
}
