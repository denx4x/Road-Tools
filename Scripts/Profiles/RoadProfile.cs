using System.Collections.Generic;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    [CreateAssetMenu(menuName = "Road Tools/Road Profile", fileName = "Road Profile")]
    public sealed class RoadProfile : ScriptableObject
    {
        [Header("Geometry")]
        [SerializeField, Min(0.5f)] private float width = 7f;
        [SerializeField, Min(0f)] private float thickness = 0.15f;
        [SerializeField, Min(0.1f)] private float sampleSpacing = 1f;
        [SerializeField, Min(1f)] private float chunkLength = 40f;

        [Header("Rendering")]
        [SerializeField] private Material material;
        [SerializeField, Min(0.01f)] private float uvMetersPerTile = 4f;

        [Header("Collision")]
        [SerializeField] private RoadColliderMode colliderMode = RoadColliderMode.Mesh;

        [Header("Terrain")]
        [SerializeField] private bool conformRoadToTerrain;
        [SerializeField] private bool deformTerrain;
        [SerializeField, Min(0f)] private float terrainBlendDistance = 2f;
        [SerializeField, Min(0f)] private float terrainSurfaceOffset = 0.05f;
        [SerializeField] private bool clearTerrainDetails;
        [SerializeField, Min(0f)] private float detailClearDistance = 0.5f;
        [SerializeField] private bool clearTerrainTrees;
        [SerializeField, Min(0f)] private float treeClearDistance = 1f;
        [SerializeField] private bool paintTerrainLayer;
        [SerializeField, Min(0)] private int terrainLayerIndex;
        [SerializeField, Min(0f)] private float terrainPaintBlendDistance = 1f;
        [SerializeField] private List<RoadTerrainPaintBand> terrainPaintBands = new();

        public float Width => width;
        public float Thickness => thickness;
        public float SampleSpacing => sampleSpacing;
        public float ChunkLength => chunkLength;
        public Material Material => material;
        public float UvMetersPerTile => uvMetersPerTile;
        public RoadColliderMode ColliderMode => colliderMode;
        public bool ConformRoadToTerrain => conformRoadToTerrain;
        public bool DeformTerrain => deformTerrain;
        public float TerrainBlendDistance => terrainBlendDistance;
        public float TerrainSurfaceOffset => terrainSurfaceOffset;
        public bool ClearTerrainDetails => clearTerrainDetails;
        public float DetailClearDistance => detailClearDistance;
        public bool ClearTerrainTrees => clearTerrainTrees;
        public float TreeClearDistance => treeClearDistance;
        public bool PaintTerrainLayer => paintTerrainLayer;
        public int TerrainLayerIndex => terrainLayerIndex;
        public float TerrainPaintBlendDistance => terrainPaintBlendDistance;
        public IReadOnlyList<RoadTerrainPaintBand> TerrainPaintBands => terrainPaintBands;
        public bool UsesTerrain => deformTerrain || clearTerrainDetails || clearTerrainTrees || paintTerrainLayer ||
                                   terrainPaintBands.Count > 0;
        public float MaxTerrainPaintReach
        {
            get
            {
                float reach = paintTerrainLayer ? terrainPaintBlendDistance : 0f;
                foreach (RoadTerrainPaintBand band in terrainPaintBands)
                    if (band != null && band.Layer != null)
                        reach = Mathf.Max(reach, band.OuterReach);
                return reach;
            }
        }
    }

    public enum RoadColliderMode
    {
        None,
        Mesh
    }
}
