using System.Collections.Generic;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    /// <summary>Resolves per-spline, per-side openings in distance along the authored path.</summary>
    public static class PropPlacementUtility
    {
        public static bool AppliesToSpline(SplinePropLayer layer, int splineIndex) =>
            layer != null && layer.Enabled &&
            (layer.SplineIndex < 0 || layer.SplineIndex == splineIndex);

        public static bool IsVisible(SplinePropLayer layer, int splineIndex, float side, float distance)
        {
            if (!IsFinite(distance) || !AppliesToSpline(layer, splineIndex) || !IncludesSide(layer.Side, side))
                return false;
            if (layer.Gaps == null)
                return true;
            foreach (PropPlacementGap gap in layer.Gaps)
            {
                if (!AppliesGap(gap, splineIndex, side))
                    continue;
                if (gap.EntireSpline)
                    return false;
                float start = Mathf.Min(gap.StartDistance, gap.EndDistance);
                float end = Mathf.Max(gap.StartDistance, gap.EndDistance);
                if (end > start && distance >= start && distance <= end)
                    return false;
            }
            return true;
        }

        public static List<Vector2> GetVisibleSpans(SplinePropLayer layer, int splineIndex,
            float side, float start, float end)
        {
            var result = new List<Vector2>();
            if (!IsFinite(start) || !IsFinite(end) || end <= start ||
                !AppliesToSpline(layer, splineIndex) || !IncludesSide(layer.Side, side))
                return result;

            var excluded = new List<Vector2>();
            if (layer.Gaps != null)
                foreach (PropPlacementGap gap in layer.Gaps)
                {
                    if (!AppliesGap(gap, splineIndex, side))
                        continue;
                    if (gap.EntireSpline)
                        return result;
                    float low = Mathf.Max(start, Mathf.Min(gap.StartDistance, gap.EndDistance));
                    float high = Mathf.Min(end, Mathf.Max(gap.StartDistance, gap.EndDistance));
                    if (high > low)
                        excluded.Add(new Vector2(low, high));
                }
            excluded.Sort((a, b) => a.x.CompareTo(b.x));
            float cursor = start;
            foreach (Vector2 gap in excluded)
            {
                if (gap.x > cursor)
                    result.Add(new Vector2(cursor, gap.x));
                cursor = Mathf.Max(cursor, gap.y);
            }
            if (cursor < end)
                result.Add(new Vector2(cursor, end));
            return result;
        }

        private static bool AppliesGap(PropPlacementGap gap, int splineIndex, float side) =>
            gap != null && (gap.EntireSpline || (IsFinite(gap.StartDistance) && IsFinite(gap.EndDistance))) &&
            (gap.SplineIndex < 0 || gap.SplineIndex == splineIndex) &&
            (gap.Side == PropSide.Both || IncludesSide(gap.Side, side));

        private static bool IncludesSide(PropSide selected, float side) => selected switch
        {
            PropSide.Left => side < 0f,
            PropSide.Right => side > 0f,
            PropSide.Center => side == 0f,
            PropSide.Both => side < 0f || side > 0f,
            _ => false
        };

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
