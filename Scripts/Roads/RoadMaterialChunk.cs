using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    // Serialized slot ownership survives baking and scene/prefab reloads.
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class RoadMaterialChunk : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int[] sectionSlots;
        [SerializeField, HideInInspector] private Material junctionMaterial;
        public void Initialize(int[] slots, Material junction = null) { sectionSlots = slots; junctionMaterial = junction; }
        public void Apply(Material mainMaterial, RoadMaterialSections sections)
        {
            if (sectionSlots == null || !TryGetComponent(out MeshRenderer renderer)) return;
            var materials = new Material[sectionSlots.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = sectionSlots[i] == -2 && junctionMaterial != null
                ? junctionMaterial : sections != null
                ? sections.GetMaterial(sectionSlots[i], mainMaterial) : mainMaterial;
            renderer.sharedMaterials = materials;
        }
    }
}
