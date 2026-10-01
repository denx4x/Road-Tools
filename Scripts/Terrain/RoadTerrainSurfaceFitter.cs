using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    internal static class RoadTerrainSurfaceFitter
    {
        // A road ribbon has only two vertices across its width. Lift the whole
        // section if terrain between those vertices rises through the surface.
        internal static void LiftSection(ref Vector3 left, ref Vector3 right, float clearance)
        {
            float width = Vector3.Distance(left, right);
            int steps = Mathf.Clamp(Mathf.CeilToInt(width / 0.35f), 2, 64);
            float lift = 0f;
            for (int step = 1; step < steps; step++)
            {
                float t = step / (float)steps;
                Vector3 point = Vector3.Lerp(left, right, t);
                if (!RoadTerrainHeightUtility.TrySampleHeight(point, out float terrainHeight))
                    continue;
                lift = Mathf.Max(lift, terrainHeight + clearance - point.y);
            }

            if (lift <= 0f)
                return;
            left.y += lift;
            right.y += lift;
        }
    }
}
