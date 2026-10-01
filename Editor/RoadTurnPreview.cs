using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal sealed class RoadTurnPreview
    {
        private int fingerprint;
        internal RoadTurnPlan Plan { get; private set; }
        internal void Invalidate() { fingerprint = 0; Plan = null; }

        internal void Update(SplineRoad road, float length, float angle, float radius)
        {
            if (road == null || road.Profile == null || !RoadDirectionPresetTool.TryGetSelectedKnot(road, out int si, out int ki))
            { Clear(); return; }
            var container = road.GetComponent<SplineContainer>();
            int hash = road.GetInstanceID();
            unchecked
            {
                hash = hash * 31 + si; hash = hash * 31 + ki;
                hash = hash * 31 + length.GetHashCode(); hash = hash * 31 + angle.GetHashCode();
                hash = hash * 31 + radius.GetHashCode(); hash = hash * 31 + road.Profile.Width.GetHashCode();
                hash = hash * 31 + container.transform.localToWorldMatrix.GetHashCode();
                hash = hash * 31 + road.IsBaked.GetHashCode();
                hash = hash * 31 + road.Profile.ConformRoadToTerrain.GetHashCode();
                hash = hash * 31 + road.AdjustTerrainToRoad.GetHashCode();
                hash = hash * 31 + road.Profile.TerrainSurfaceOffset.GetHashCode();
                foreach (Spline spline in container.Splines)
                for (int i = 0; i < spline.Count; i++)
                {
                    hash = hash * 31 + spline[i].GetHashCode();
                    hash = hash * 31 + (int)spline.GetTangentMode(i);
                }
            }
            if (Plan != null && hash == fingerprint) return;
            fingerprint = hash; Plan = RoadTurnPlan.Create(road, length, angle, radius);
            SceneView.RepaintAll();
        }
        internal void Clear() { Plan = null; }
        internal void Draw()
        {
            if (Plan?.Center == null || Event.current.type != EventType.Repaint) return;
            Color previous = Handles.color;
            var depth = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.color = Plan.Safe ? new Color(0.1f, 0.9f, 0.55f, 0.9f) : new Color(1f, 0.2f, 0.15f, 0.9f);
            Handles.DrawAAPolyLine(3f, Plan.Center);
            Handles.DrawAAPolyLine(2f, Plan.Left); Handles.DrawAAPolyLine(2f, Plan.Right);
            for (int i = 0; i < Plan.Center.Length; i += 12) Handles.DrawLine(Plan.Left[i], Plan.Right[i]);
            Handles.Label(Plan.Center[Plan.Center.Length-1], Plan.Safe ? "TURN PREVIEW - Apply in Road Tools" : "OVERLAP - adjust turn");
            Handles.color = previous; Handles.zTest = depth;
        }
    }
}
