using System;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    [Serializable]
    public sealed class PropPlacementGap
    {
        [SerializeField, Min(0f)] private float startDistance;
        [SerializeField, Min(0f)] private float endDistance = 3f;
        [SerializeField] private PropSide side = PropSide.Both;
        [SerializeField] private int splineIndex = -1;
        [SerializeField] private bool entireSpline;

        public float StartDistance => startDistance;
        public float EndDistance => endDistance;
        public PropSide Side => side;
        public int SplineIndex => splineIndex;
        public bool EntireSpline => entireSpline;

        public PropPlacementGap() { }

        public PropPlacementGap(float start, float end, PropSide selectedSide = PropSide.Both,
            int selectedSplineIndex = -1, bool entireSpline = false)
        {
            startDistance = start;
            endDistance = end;
            side = selectedSide;
            splineIndex = selectedSplineIndex;
            this.entireSpline = entireSpline;
        }

        public PropPlacementGap Clone() => (PropPlacementGap)MemberwiseClone();
    }
}
