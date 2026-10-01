using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    [DisallowMultipleComponent]
    public sealed class RoadIntersection : MonoBehaviour
    {
        [SerializeField] private RoadSocket[] sockets;

        public RoadSocket[] Sockets => sockets;

        public void RefreshSockets()
        {
            sockets = GetComponentsInChildren<RoadSocket>(true);
        }
    }
}
