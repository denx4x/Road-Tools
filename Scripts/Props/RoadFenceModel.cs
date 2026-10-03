using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    /// <summary>Describes an imported fence segment that can bend continuously along a road.</summary>
    [DisallowMultipleComponent]
    public sealed class RoadFenceModel : MonoBehaviour
    {
        public enum LengthAxis { Z, X }

        [SerializeField] private LengthAxis longitudinalAxis = LengthAxis.Z;
        [SerializeField] private bool mirrorRightSide = true;
        [SerializeField, Tooltip("Reverse the model's lateral facing before left/right mirroring. Enable if support posts face the road instead of the rail.")]
        private bool flipFacing;
        [SerializeField, Min(0.1f)] private float meshSubdivisionLength = 0.4f;
        [SerializeField, Min(0f), Tooltip("Distance in meters used to smooth the fence height over terrain.")]
        private float heightSmoothingDistance = 4f;
        [SerializeField, Min(0f), Tooltip("Height above the model bottom below which supports stretch to the ground. Set to zero for models without separate supports.")]
        private float groundedSupportHeight;

        public LengthAxis LongitudinalAxis => longitudinalAxis;
        public bool MirrorRightSide => mirrorRightSide;
        public bool FlipFacing => flipFacing;
        public float MeshSubdivisionLength => Mathf.Max(0.1f, meshSubdivisionLength);
        public float HeightSmoothingDistance => Mathf.Max(0f, heightSmoothingDistance);
        public float GroundedSupportHeight => Mathf.Max(0f, groundedSupportHeight);

        public float Longitudinal(Vector3 value) => longitudinalAxis == LengthAxis.Z ? value.z : value.x;
        public float Across(Vector3 value) => longitudinalAxis == LengthAxis.Z ? value.x : -value.z;

        public bool TryGetRootBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            Matrix4x4 rootScale = Matrix4x4.Scale(transform.localScale);
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled)
                    continue;
                Matrix4x4 matrix = rootScale * transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds meshBounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    point = matrix.MultiplyPoint3x4(point);
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return found && Longitudinal(bounds.size) > 0.01f;
        }

        public float GetSegmentLength() => TryGetRootBounds(out Bounds bounds) ? Longitudinal(bounds.size) : 0f;
    }
}
