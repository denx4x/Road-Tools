using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    // A junction preserves separate spline branches instead of concatenating them.
    internal static class RoadKnotJunctionGeometry
    {
        internal static bool Validate(RoadKnotConnectionReference a, RoadKnotConnectionReference b, out string message)
        {
            message = "Select knot A, then knot B. Any knot can be a junction, including internal knots.";
            if (a == null || b == null) return false;
            if (!a.TryResolve(out Spline sourceA, out SplineRoad roadA, out message)) return false;
            if (!b.TryResolve(out Spline sourceB, out SplineRoad roadB, out message)) return false;
            if (sourceA == sourceB)
            { message = "Choose knots from different splines."; return false; }
            if (sourceA.Count < 2 || sourceB.Count < 2)
            { message = "Both selected splines need at least two knots."; return false; }
            if (!a.Container.gameObject.scene.IsValid() || !a.Container.gameObject.scene.isLoaded ||
                !b.Container.gameObject.scene.IsValid() || !b.Container.gameObject.scene.isLoaded ||
                a.Container.gameObject.scene != b.Container.gameObject.scene)
            { message = "Both roads must belong to the same scene."; return false; }
            if (roadA.IsBaked || roadB.IsBaked)
            { message = "Resume editing both roads before joining knots."; return false; }
            if (!roadA.isActiveAndEnabled || !roadB.isActiveAndEnabled || !roadA.LiveUpdatesEnabled || !roadB.LiveUpdatesEnabled)
            { message = "Enable both roads and their live updates before joining knots."; return false; }
            if (Mathf.Abs(a.Container.transform.localToWorldMatrix.determinant) < 0.00001f ||
                Mathf.Abs(b.Container.transform.localToWorldMatrix.determinant) < 0.00001f)
            { message = "Restore nonzero road transform scales before joining knots."; return false; }
            if (a.Container != b.Container && Mathf.Abs(roadA.Profile.Width - roadB.Profile.Width) > 0.01f)
            { message = "Match the road widths before joining different road objects."; return false; }
            var knotA = new SplineKnotIndex(a.SplineIndex, a.KnotIndex);
            var knotB = new SplineKnotIndex(b.SplineIndex, b.KnotIndex);
            if (a.Container == b.Container && a.Container.AreKnotLinked(knotA, knotB))
            { message = "These knots already share a junction."; return false; }
            message = (RoadKnotJunctionBridge.NeedsBridge(a.Position, b.Position)
                ? "An editable connector joins A to B without moving source knots. Each end shares its source knot. "
                : "Coincident knots are linked; A snaps to B within 1 cm. ") +
                "All branches remain; moving a shared knot moves its linked knots. " +
                (a.Container == b.Container ? "Both splines stay in this road." :
                 "All splines from road B move into road A, using A's profile, main material and prop layers. Local material sections are preserved. Imported handles become Bezier to preserve their curves.") +
                " Linked junctions trim overlapping surfaces and open props automatically; the junction material removes crossing markings.";
            return true;
        }

        internal static Spline CopyTransformed(Spline source, Matrix4x4 matrix)
        {
            var result = new Spline(source.Count, source.Closed);
            for (int i = 0; i < source.Count; i++)
            {
                BezierKnot original = source[i];
                Vector3 forward = matrix.MultiplyVector((Vector3)math.rotate(original.Rotation, new float3(0, 0, 1)));
                Vector3 up = matrix.MultiplyVector((Vector3)math.rotate(original.Rotation, new float3(0, 1, 0)));
                Quaternion rotation = Quaternion.LookRotation(forward, up);
                Quaternion inverse = Quaternion.Inverse(rotation);
                Vector3 incoming = matrix.MultiplyVector((Vector3)math.rotate(original.Rotation, original.TangentIn));
                Vector3 outgoing = matrix.MultiplyVector((Vector3)math.rotate(original.Rotation, original.TangentOut));
                result.Add(new BezierKnot((float3)matrix.MultiplyPoint3x4((Vector3)original.Position),
                    (float3)(inverse * incoming), (float3)(inverse * outgoing),
                    new quaternion(rotation.x, rotation.y, rotation.z, rotation.w)), TangentMode.Broken);
            }
            RoadKnotConnectionSplineData.Copy(source, result, false, 0);
            return result;
        }
    }
}
