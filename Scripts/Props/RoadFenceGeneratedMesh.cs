using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    /// <summary>Owns only a generated mesh, never the imported fence's shared mesh.</summary>
    [DisallowMultipleComponent]
    public sealed class RoadFenceGeneratedMesh : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Mesh generatedMesh;

        internal void Initialize(Mesh mesh) => generatedMesh = mesh;

        public void Release()
        {
            Mesh mesh = generatedMesh;
            generatedMesh = null;
            if (mesh == null) return;
            if (TryGetComponent(out MeshFilter filter) && filter.sharedMesh == mesh)
                filter.sharedMesh = null;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        private void OnDestroy() => Release();
    }
}
