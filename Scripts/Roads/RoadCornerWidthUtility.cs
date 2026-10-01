using System.Collections.Generic;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    public static class RoadCornerWidthUtility
    {
        private const float InnerEdgeSafetyFactor = 0.9f;

        public static bool HasTightCorner(IReadOnlyList<SplineSamplingUtility.Sample> samples, float roadWidth)
        {
            if (samples == null || samples.Count < 3)
                return false;

            float halfWidth = Mathf.Max(0f, roadWidth * 0.5f);
            for (int i = 1; i < samples.Count - 1; i++)
            {
                if (TryGetTurnRadius(samples, i, out _, out float turnRadius) && turnRadius < halfWidth)
                    return true;
            }

            return false;
        }

        public static int CountTightCorners(IReadOnlyList<SplineSamplingUtility.Sample> samples, float roadWidth)
        {
            if (samples == null || samples.Count < 3)
                return 0;

            float halfWidth = Mathf.Max(0f, roadWidth * 0.5f);
            int count = 0;
            for (int i = 1; i < samples.Count - 1; i++)
            {
                if (TryGetTurnRadius(samples, i, out _, out float turnRadius) && turnRadius < halfWidth)
                    count++;
            }
            return count;
        }

        public static void EvaluateSideWidths(
            IReadOnlyList<SplineSamplingUtility.Sample> samples,
            float distance,
            float roadWidth,
            out float leftWidth,
            out float rightWidth)
        {
            float halfWidth = Mathf.Max(0f, roadWidth * 0.5f);
            leftWidth = halfWidth;
            rightWidth = halfWidth;

            if (samples == null || samples.Count < 3 || halfWidth <= 0f)
                return;

            float totalLength = samples[samples.Count - 1].Distance;
            float transitionDistance = Mathf.Max(0.5f, halfWidth);
            int first = FindFirstAtOrAfter(samples, Mathf.Max(0f, distance - transitionDistance));
            int last = FindFirstAtOrAfter(samples, Mathf.Min(totalLength, distance + transitionDistance));

            for (int i = Mathf.Max(1, first); i <= Mathf.Min(samples.Count - 2, last); i++)
            {
                if (!TryGetTurnRadius(samples, i, out float signedTurnRadians, out float turnRadius))
                    continue;
                if (turnRadius >= halfWidth)
                    continue;

                float distanceFromCorner = Mathf.Abs(samples[i].Distance - distance);
                float influence = 1f - Mathf.Clamp01(distanceFromCorner / transitionDistance);
                influence = influence * influence * (3f - 2f * influence);
                float safeInnerWidth = Mathf.Max(0.001f, turnRadius * InnerEdgeSafetyFactor);
                float blendedWidth = Mathf.Lerp(halfWidth, safeInnerWidth, influence);
                if (signedTurnRadians < 0f)
                    leftWidth = Mathf.Min(leftWidth, blendedWidth);
                else
                    rightWidth = Mathf.Min(rightWidth, blendedWidth);
            }
        }

        private static bool TryGetTurnRadius(
            IReadOnlyList<SplineSamplingUtility.Sample> samples,
            int index,
            out float signedTurnRadians,
            out float turnRadius)
        {
            signedTurnRadians = 0f;
            turnRadius = float.PositiveInfinity;
            SplineSamplingUtility.Sample before = samples[index - 1];
            SplineSamplingUtility.Sample after = samples[index + 1];
            Vector3 beforeTangent = Vector3.ProjectOnPlane(before.Tangent, Vector3.up).normalized;
            Vector3 afterTangent = Vector3.ProjectOnPlane(after.Tangent, Vector3.up).normalized;
            if (beforeTangent.sqrMagnitude < 0.5f || afterTangent.sqrMagnitude < 0.5f)
                return false;

            signedTurnRadians = Vector3.SignedAngle(beforeTangent, afterTangent, Vector3.up) * Mathf.Deg2Rad;
            float absoluteTurnRadians = Mathf.Abs(signedTurnRadians);
            float distanceSpan = after.Distance - before.Distance;
            if (absoluteTurnRadians < 0.01f || distanceSpan <= 0.001f)
                return false;

            turnRadius = distanceSpan / absoluteTurnRadians;
            return true;
        }

        private static int FindFirstAtOrAfter(IReadOnlyList<SplineSamplingUtility.Sample> samples, float distance)
        {
            int low = 0;
            int high = samples.Count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (samples[middle].Distance < distance)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }
    }
}
