using System;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    public enum RoadMaterialRangeMode { Distance, Knots }

    [Serializable]
    public sealed class RoadMaterialSection
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private Material material;
        [SerializeField, Min(0)] private int splineIndex;
        [SerializeField] private RoadMaterialRangeMode rangeMode;
        [SerializeField, Min(0f)] private float startDistance;
        [SerializeField, Min(0f)] private float endDistance = 10f;
        [SerializeField, Min(0)] private int startKnot;
        [SerializeField, Min(0)] private int endKnot = 1;

        public bool Enabled => enabled;
        public Material Material => material;
        public int SplineIndex => splineIndex;
        public RoadMaterialRangeMode RangeMode => rangeMode;
        public float StartDistance => startDistance;
        public float EndDistance => endDistance;
        public int StartKnot => startKnot;
        public int EndKnot => endKnot;

        public RoadMaterialSection(Material value, int spline, float start, float end)
        { material = value; splineIndex = spline; startDistance = start; endDistance = end; }

        public static RoadMaterialSection BetweenKnots(Material value, int spline, int start, int end) =>
            new RoadMaterialSection(value, spline, 0, 0) { rangeMode = RoadMaterialRangeMode.Knots, startKnot = start, endKnot = end };
    }

    public readonly struct RoadMaterialSpan
    {
        public readonly float Start, End;
        public readonly int SectionIndex;
        public RoadMaterialSpan(float start, float end, int section) { Start = start; End = end; SectionIndex = section; }
    }
}
