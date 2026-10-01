using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    [DisallowMultipleComponent]
    public sealed class RoadSocket : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float snapRadius = 3f;
        [SerializeField, Min(0.1f)] private float tangentLength = 4f;
        [SerializeField, Min(1)] private int laneCount = 2;
        [SerializeField, Min(0.5f)] private float roadWidth = 7f;
        [SerializeField] private string category = "Road";
        [SerializeField] private bool occupied;
        [SerializeField] private SplineRoad connectedRoad;
        [SerializeField] private int connectedSplineIndex = -1;
        [SerializeField] private bool connectedAtStart;

        public float SnapRadius => snapRadius;
        public float TangentLength => tangentLength;
        public int LaneCount => laneCount;
        public float RoadWidth => roadWidth;
        public string Category => category;
        public bool Occupied => occupied;
        public SplineRoad ConnectedRoad => connectedRoad;

        public void SetOccupied(bool value) => occupied = value;

        public void Connect(SplineRoad road, int splineIndex, bool atStart)
        {
            connectedRoad = road;
            connectedSplineIndex = splineIndex;
            connectedAtStart = atStart;
            occupied = road != null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = occupied ? Color.red : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, snapRadius);

            Vector3 halfWidth = transform.right * (roadWidth * 0.5f);
            Vector3 left = transform.position - halfWidth;
            Vector3 right = transform.position + halfWidth;
            Vector3 forward = transform.position + transform.forward * tangentLength;
            Gizmos.DrawLine(left, right);
            Gizmos.DrawLine(left, left + transform.forward * tangentLength);
            Gizmos.DrawLine(right, right + transform.forward * tangentLength);
            Gizmos.DrawLine(left + transform.forward * tangentLength, right + transform.forward * tangentLength);
            Gizmos.DrawLine(transform.position, forward);
            Gizmos.DrawLine(forward, forward - transform.right * (roadWidth * 0.15f) + transform.up * (roadWidth * 0.15f));
            Gizmos.DrawLine(forward, forward + transform.right * (roadWidth * 0.15f) + transform.up * (roadWidth * 0.15f));
        }
    }
}
