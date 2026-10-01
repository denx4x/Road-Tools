using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadEndpointSnapUtility
    {
        public static string Snap(SplineRoad road, RoadSocket socket)
        {
            if (road == null || socket == null)
                return "Select a road and target socket.";
            if (road.IsBaked)
                return "Resume editing before snapping a baked road.";
            if (socket.Occupied)
                return "Socket is already connected.";

            SplineContainer container = road.GetComponent<SplineContainer>();
            if (container == null)
                return "Road has no spline container.";

            int nearestSpline = -1;
            int nearestKnot = -1;
            float bestDistance = float.PositiveInfinity;
            for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
            {
                Spline spline = container.Splines[splineIndex];
                if (spline == null || spline.Count < 2)
                    continue;
                int last = spline.Count - 1;
                for (int endpoint = 0; endpoint < 2; endpoint++)
                {
                    int knotIndex = endpoint == 0 ? 0 : last;
                    Vector3 position = container.transform.TransformPoint((Vector3)spline[knotIndex].Position);
                    float distance = Vector3.Distance(position, socket.transform.position);
                    if (distance >= bestDistance)
                        continue;
                    bestDistance = distance;
                    nearestSpline = splineIndex;
                    nearestKnot = knotIndex;
                }
            }

            if (nearestSpline < 0)
                return "Road needs a spline with at least two knots.";
            if (bestDistance > socket.SnapRadius)
                return $"Nearest endpoint is {bestDistance:0.##} m away; socket radius is {socket.SnapRadius:0.##} m.";

            Spline selectedSpline = container.Splines[nearestSpline];
            bool atStart = nearestKnot == 0;
            Undo.RecordObject(container, "Snap Road Endpoint");
            Undo.RecordObject(socket, "Connect Road Socket");
            BezierKnot knot = selectedSpline[nearestKnot];
            knot.Position = container.transform.InverseTransformPoint(socket.transform.position);
            Quaternion worldRotation = atStart
                ? Quaternion.LookRotation(-socket.transform.forward, socket.transform.up)
                : Quaternion.LookRotation(socket.transform.forward, socket.transform.up);
            Quaternion localRotation = Quaternion.Inverse(container.transform.rotation) * worldRotation;
            knot.Rotation = new quaternion(localRotation.x, localRotation.y, localRotation.z, localRotation.w);
            selectedSpline[nearestKnot] = knot;
            selectedSpline.SetTangentMode(nearestKnot, TangentMode.AutoSmooth);
            socket.Connect(road, nearestSpline, atStart);
            EditorUtility.SetDirty(container);
            EditorUtility.SetDirty(socket);
            RoadBuildReport build = road.Rebuild();
            EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
            return build.Succeeded
                ? $"Snapped spline {nearestSpline + 1} {(atStart ? "start" : "end")} to {socket.name}. {build.Message}"
                : build.Message;
        }
    }
}
