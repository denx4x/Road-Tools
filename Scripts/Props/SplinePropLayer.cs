using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit
{
    [Serializable]
    public sealed class SplinePropLayer
    {
        [SerializeField] private string name = "Prop Layer";
        [SerializeField] private GameObject prefab;
        [SerializeField] private PropSide side = PropSide.Both;
        [SerializeField] private PropGroundingMode grounding = PropGroundingMode.RoadShoulder;
        [SerializeField, Min(0.1f)] private float spacing = 8f;
        [SerializeField, Min(0f)] private float startOffset;
        [SerializeField, Min(0f)] private float endOffset;
        [SerializeField] private float lateralOffset = 1f;
        [SerializeField] private float verticalOffset;
        [SerializeField] private bool followSplineRotation = true;
        [SerializeField] private Vector3 rotationOffset;
        [SerializeField] private Vector3 randomRotation;
        [SerializeField] private Vector2 randomScale = Vector2.one;
        [SerializeField] private int seed = 12345;
        [SerializeField] private float intersectionExclusionDistance = 3f;
        [SerializeField] private bool continuousFence;
        [SerializeField] private bool enabled = true;
        [SerializeField] private int splineIndex = -1;
        [SerializeField] private SplineContainer customPath;
        [SerializeField, Min(0)] private int customSplineIndex;
        [SerializeField] private bool useCustomPath;
        [SerializeField] private bool mirrorCustomPath;
        [SerializeField] private List<PropPlacementGap> gaps = new();

        public string Name => string.IsNullOrWhiteSpace(name) ? "Prop Layer" : name;
        public GameObject Prefab => prefab;
        public PropSide Side => side;
        public PropGroundingMode Grounding => grounding;
        public float Spacing => spacing;
        public float StartOffset => startOffset;
        public float EndOffset => endOffset;
        public float LateralOffset => lateralOffset;
        public float VerticalOffset => verticalOffset;
        public bool FollowSplineRotation => followSplineRotation;
        public Vector3 RotationOffset => rotationOffset;
        public Vector3 RandomRotation => randomRotation;
        public Vector2 RandomScale => randomScale;
        public int Seed => seed;
        public float IntersectionExclusionDistance => intersectionExclusionDistance;
        public bool ContinuousFence => continuousFence;
        public bool Enabled => enabled;
        public int SplineIndex => splineIndex;
        public SplineContainer CustomPath => customPath;
        public int CustomSplineIndex => customSplineIndex;
        public bool UsesCustomPath => useCustomPath || customPath != null;
        public bool MirrorCustomPath => mirrorCustomPath;
        public IReadOnlyList<PropPlacementGap> Gaps => gaps;

        public SplinePropLayer Clone()
        {
            var copy = (SplinePropLayer)MemberwiseClone();
            copy.gaps = new List<PropPlacementGap>();
            if (gaps != null)
                foreach (PropPlacementGap gap in gaps)
                    copy.gaps.Add(gap?.Clone());
            return copy;
        }
    }

    public enum PropSide
    {
        Left,
        Right,
        Both,
        Center
    }

    public enum PropGroundingMode
    {
        RoadShoulder,
        Terrain
    }
}
