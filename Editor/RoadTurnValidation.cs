using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadTurnValidation
    {
        internal static void SampleAndValidate(RoadTurnPlan plan, SplineContainer container, float width)
        {
            int count = Mathf.Clamp(plan.Spline.Count * 48, 96, 2048);
            plan.Center = new Vector3[count + 1];
            plan.Left = new Vector3[count + 1]; plan.Right = new Vector3[count + 1];
            var distance = new float[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count;
                Vector3 p = container.transform.TransformPoint((Vector3)SplineUtility.EvaluatePosition(plan.Spline, t));
                Vector3 tangent = container.transform.TransformVector((Vector3)SplineUtility.EvaluateTangent(plan.Spline, t));
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized * width * 0.5f;
                plan.Center[i] = p; plan.Left[i] = p - side; plan.Right[i] = p + side;
                if (i > 0) distance[i] = distance[i - 1] + Vector3.Distance(p, plan.Center[i - 1]);
            }
            plan.Safe = true; plan.Message = "Preview ready. Length is the straight section after the bend. Apply to build.";
            for (int i = 0; i < count; i++)
            for (int j = i + 2; j < count; j++)
            {
                if (distance[j] - distance[i + 1] < width * 1.5f) continue;
                if (Mathf.Abs(plan.Center[i].y - plan.Center[j].y) > 1f) continue;
                if (Crosses(plan.Left[i], plan.Left[i+1], plan.Left[j], plan.Left[j+1]) ||
                    Crosses(plan.Right[i], plan.Right[i+1], plan.Right[j], plan.Right[j+1]) ||
                    Crosses(plan.Left[i], plan.Left[i+1], plan.Right[j], plan.Right[j+1]) ||
                    Crosses(plan.Right[i], plan.Right[i+1], plan.Left[j], plan.Left[j+1]) ||
                    Crosses(plan.Center[i], plan.Center[i+1], plan.Center[j], plan.Center[j+1]) ||
                    Separation(plan.Center[i], plan.Center[i+1], plan.Center[j], plan.Center[j+1]) < width - 0.05f)
                { plan.Safe = false; plan.Message = "Road footprint overlaps. Change angle, radius or length, or resolve existing overlaps."; return; }
            }
            for (int si = 0; si < container.Splines.Count; si++)
            {
                if (si == plan.SplineIndex || container.Splines[si].Count < 2) continue;
                var other = container.Splines[si];
                int steps = Mathf.Clamp(other.Count * 48, 96, 2048);
                Vector3 previous = container.transform.TransformPoint((Vector3)SplineUtility.EvaluatePosition(other, 0));
                for (int j = 1; j <= steps; j++)
                {
                    Vector3 next = container.transform.TransformPoint((Vector3)SplineUtility.EvaluatePosition(other, j/(float)steps));
                    for (int i = 0; i < count; i++)
                    {
                        if (Mathf.Abs(plan.Center[i].y - previous.y) > 1f) continue;
                        if (Crosses(plan.Center[i], plan.Center[i+1], previous, next) ||
                            Separation(plan.Center[i], plan.Center[i+1], previous, next) < width - 0.05f)
                        { plan.Safe = false; plan.Message = "Preview overlaps another spline in this road container."; return; }
                    }
                    previous = next;
                }
            }
        }
        private static float Separation(Vector3 a, Vector3 b, Vector3 c, Vector3 d) => Mathf.Min(
            Mathf.Min(Distance(a,c,d), Distance(b,c,d)), Mathf.Min(Distance(c,a,b), Distance(d,a,b)));
        private static float Distance(Vector3 p, Vector3 a, Vector3 b)
        {
            p.y = a.y = b.y = 0;
            Vector3 delta = b-a;
            float t = delta.sqrMagnitude < 0.000001f ? 0 : Mathf.Clamp01(Vector3.Dot(p-a,delta)/delta.sqrMagnitude);
            return Vector3.Distance(p,a+delta*t);
        }
        private static float Side(Vector3 a, Vector3 b, Vector3 p) => (b.x-a.x)*(p.z-a.z)-(b.z-a.z)*(p.x-a.x);
        private static bool Crosses(Vector3 a, Vector3 b, Vector3 c, Vector3 d) =>
            Side(a,b,c)*Side(a,b,d) < -0.000001f && Side(c,d,a)*Side(c,d,b) < -0.000001f;
    }
}
