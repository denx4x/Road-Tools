using System;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    [Serializable]
    public sealed class RoadTerrainPaintBand
    {
        [SerializeField] private TerrainLayer layer;
        [SerializeField, Min(0f)] private float startDistance;
        [SerializeField, Min(0.1f)] private float width = 3f;
        [SerializeField, Min(0f)] private float blendDistance = 1f;

        public TerrainLayer Layer => layer;
        public float StartDistance => startDistance;
        public float Width => width;
        public float BlendDistance => blendDistance;
        public float EndDistance => startDistance + width;
        public float OuterReach => EndDistance + blendDistance * 0.5f;
    }
}
