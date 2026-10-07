using System;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    [Serializable]
    internal sealed class RoadKnotConnectionReference
    {
        [SerializeField] private SplineContainer container;
        [SerializeField] private int splineIndex = -1;
        [SerializeField] private int knotIndex = -1;
        [SerializeField] private int splineCount;
        [SerializeField] private int knotCount;
        [NonSerialized] private Spline capturedSpline;

        internal SplineContainer Container => container;
        internal int SplineIndex => splineIndex;
        internal int KnotIndex => knotIndex;

        internal RoadKnotConnectionReference(SplineContainer source, int spline, int knot)
        {
            container = source;
            splineIndex = spline;
            knotIndex = knot;
            if (source == null || spline < 0 || spline >= source.Splines.Count) return;
            capturedSpline = source.Splines[spline];
            splineCount = source.Splines.Count;
            knotCount = capturedSpline.Count;
        }

        internal bool TryResolve(out Spline spline, out SplineRoad road, out string error)
        {
            spline = null;
            road = null;
            error = "Select a road knot in Scene View.";
            if (container == null) return false;
            if (container.Splines.Count != splineCount || splineIndex < 0 || splineIndex >= container.Splines.Count)
            {
                error = "The spline collection changed. Capture this knot again.";
                return false;
            }
            spline = container.Splines[splineIndex];
            if (spline == null || spline.Count != knotCount || knotIndex < 0 || knotIndex >= spline.Count ||
                (capturedSpline != null && capturedSpline != spline))
            {
                error = "Knots were added, removed or replaced. Capture this knot again.";
                return false;
            }
            road = container.GetComponent<SplineRoad>();
            if (road == null || road.Profile == null)
            {
                error = "Set up Road Tools and assign a Road Profile on both roads.";
                return false;
            }
            error = null;
            return true;
        }

        internal bool Matches(RoadKnotConnectionReference other) => other != null &&
            container == other.container && splineIndex == other.splineIndex && knotIndex == other.knotIndex;

        internal string Label => container == null ? "Not selected" :
            $"{container.name}  /  S{splineIndex + 1} · P{knotIndex + 1}";

        internal Vector3 Position => container != null && TryResolve(out Spline spline, out _, out _)
            ? container.transform.TransformPoint((Vector3)spline[knotIndex].Position) : Vector3.zero;
    }
}
