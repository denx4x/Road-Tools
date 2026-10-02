using System.Collections.Generic;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    /// <summary>Preserves a rail's cross section while grounding only its lower supports.</summary>
    internal sealed class RoadFenceDeformation
    {
        private readonly RoadFencePath path;
        private readonly float supportHeight;
        private readonly Dictionary<float, Frame> frames = new Dictionary<float, Frame>();

        private struct Frame
        {
            internal Vector3 Position, Right, Derivative, RightDerivative;
            internal float GroundDelta, GroundDeltaDerivative;
        }

        internal RoadFenceDeformation(RoadFencePath path, float supportHeight)
        {
            this.path = path;
            this.supportHeight = supportHeight;
        }

        internal Vector3 Transform(float distance, float across, float height, out Matrix4x4 basis)
        {
            if (!frames.TryGetValue(distance, out Frame frame))
            {
                frame = EvaluateFrame(distance);
                frames.Add(distance, frame);
            }
            float weight = supportHeight > 0f ? Mathf.Clamp01(1f - height / supportHeight) : 0f;
            float heightScale = weight > 0f && height >= 0f ? 1f - frame.GroundDelta / supportHeight : 1f;
            Vector3 along = frame.Derivative + frame.RightDerivative * across +
                Vector3.up * (weight * frame.GroundDeltaDerivative);
            // The inverse transpose of this Jacobian keeps imported hard edges and UV seams.
            basis = Matrix4x4.identity;
            basis.SetColumn(0, new Vector4(frame.Right.x, frame.Right.y, frame.Right.z, 0f));
            basis.SetColumn(1, new Vector4(0f, Mathf.Max(0.05f, heightScale), 0f, 0f));
            basis.SetColumn(2, new Vector4(along.x, along.y, along.z, 0f));
            return frame.Position + frame.Right * across + Vector3.up * (height + weight * frame.GroundDelta);
        }

        private Frame EvaluateFrame(float distance)
        {
            const float delta = 0.05f;
            Frame result = default;
            path.Evaluate(distance, out result.Position, out result.Right, out result.Derivative);
            float beforeDistance = Mathf.Max(path.Start, distance - delta);
            float afterDistance = Mathf.Min(path.End, distance + delta);
            float span = afterDistance - beforeDistance;
            path.Evaluate(beforeDistance, out Vector3 before, out Vector3 beforeRight, out _);
            path.Evaluate(afterDistance, out Vector3 after, out Vector3 afterRight, out _);
            float ground = path.GroundHeight(distance);
            float beforeGround = path.GroundHeight(beforeDistance);
            float afterGround = path.GroundHeight(afterDistance);
            if (supportHeight > 0f)
            {
                // Keep a positive support length even on terrain peaks taller than the smoothing window.
                float limit = supportHeight * 0.8f;
                result.Position.y = Mathf.Max(result.Position.y, ground - limit);
                before.y = Mathf.Max(before.y, beforeGround - limit);
                after.y = Mathf.Max(after.y, afterGround - limit);
            }
            result.GroundDelta = ground - result.Position.y;
            if (span > 0.0001f)
            {
                result.Derivative = (after - before) / span;
                result.RightDerivative = (afterRight - beforeRight) / span;
                result.GroundDeltaDerivative = ((afterGround - after.y) - (beforeGround - before.y)) / span;
            }
            return result;
        }
    }
}
