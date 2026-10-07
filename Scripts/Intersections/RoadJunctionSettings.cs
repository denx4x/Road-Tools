using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    [DisallowMultipleComponent]
    public sealed class RoadJunctionSettings : MonoBehaviour
    {
        [SerializeField] private Material surfaceMaterial;
        public Material SurfaceMaterial => surfaceMaterial;
        public void SetSurfaceMaterial(Material value) { surfaceMaterial = value; }
        private void OnValidate() => GetComponent<SplineRoad>()?.RequestRebuild();
    }
}
