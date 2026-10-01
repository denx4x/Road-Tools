using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    [DisallowMultipleComponent]
    public sealed class RoadColliderGenerator : MonoBehaviour
    {
        public int LastGeneratedColliderCount { get; private set; }

        public void Rebuild(Transform generatedRoadRoot, RoadProfile profile)
        {
            LastGeneratedColliderCount = 0;
            if (generatedRoadRoot == null || profile == null || profile.ColliderMode == RoadColliderMode.None)
                return;

            foreach (MeshFilter filter in generatedRoadRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;

                MeshCollider collider = filter.GetComponent<MeshCollider>();
                if (collider == null)
                    collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                LastGeneratedColliderCount++;
            }
        }
    }
}
