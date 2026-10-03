using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit
{
    [DisallowMultipleComponent]
    public sealed class PropLayerManager : MonoBehaviour
    {
        private const string GeneratedRootName = "Generated Props";
        [SerializeField] private List<SplinePropLayer> layers = new();
        [SerializeField] private bool propsEnabled = true;

        public IReadOnlyList<SplinePropLayer> Layers => layers;
        public bool PropsEnabled => propsEnabled;
        public int LastGeneratedInstanceCount { get; private set; }

        public void AddLayer() => layers.Add(new SplinePropLayer());

        public int DuplicateLayer(int index)
        {
            if (index < 0 || index >= layers.Count || layers[index] == null)
                return -1;
            layers.Add(layers[index].Clone());
            return layers.Count - 1;
        }

        public void RemoveLayer(int index)
        {
            if (index >= 0 && index < layers.Count)
                layers.RemoveAt(index);
        }

        public float GetRoadShoulderRadius(RoadProfile profile)
        {
            float baseHalfWidth = profile != null ? profile.Width * 0.5f : 0f;
            float radius = baseHalfWidth;
            if (!propsEnabled)
                return radius;
            foreach (SplinePropLayer layer in layers)
            {
                if (layer == null || !layer.Enabled || layer.UsesCustomPath || layer.Prefab == null ||
                    layer.Grounding != PropGroundingMode.RoadShoulder)
                    continue;
                float lateral = layer.Side == PropSide.Center
                    ? Mathf.Abs(layer.LateralOffset)
                    : Mathf.Abs(baseHalfWidth + layer.LateralOffset);
                radius = Mathf.Max(radius, lateral + 0.25f);
            }
            return radius;
        }

        public void Rebuild(SplineContainer spline, RoadProfile profile)
        {
            LastGeneratedInstanceCount = 0;
            ClearGenerated();
            if (!propsEnabled || spline == null || profile == null)
                return;
            var root = new GameObject(GeneratedRootName);
            root.transform.SetParent(transform, false);

            for (int splineIndex = 0; splineIndex < spline.Splines.Count; splineIndex++)
            {
                List<SplineSamplingUtility.Sample> samples = BuildSamples(spline, splineIndex, profile, null);
                if (samples == null) continue;
                float length = samples[samples.Count - 1].Distance;
                foreach (SplinePropLayer layer in layers)
                    if (layer != null && !layer.UsesCustomPath)
                        BuildLayer(root.transform, samples, length, profile, layer, splineIndex);
            }

            // A custom fence path is authored independently; do not repeat it for every road spline.
            foreach (SplinePropLayer layer in layers)
            {
                if (layer == null || !layer.UsesCustomPath || layer.CustomPath == null || !layer.Enabled)
                    continue;
                List<SplineSamplingUtility.Sample> samples =
                    BuildSamples(layer.CustomPath, layer.CustomSplineIndex, profile, layer);
                if (samples != null)
                    BuildLayer(root.transform, samples, samples[samples.Count - 1].Distance,
                        profile, layer, layer.CustomSplineIndex);
            }
        }

        private List<SplineSamplingUtility.Sample> BuildSamples(SplineContainer container, int splineIndex,
            RoadProfile profile, SplinePropLayer customLayer)
        {
            if (splineIndex < 0 || splineIndex >= container.Splines.Count ||
                container.Splines[splineIndex] == null || container.Splines[splineIndex].Count < 2)
                return null;
            float sampleSpacing = Mathf.Max(0.5f, FiniteOr(profile.SampleSpacing, 0.5f));
            foreach (SplinePropLayer layer in layers)
            {
                if (layer == null || !PropPlacementUtility.AppliesToSpline(layer, splineIndex) ||
                    (customLayer == null ? layer.UsesCustomPath : layer != customLayer))
                    continue;
                if (layer.ContinuousFence && layer.Prefab != null &&
                    layer.Prefab.GetComponent<RoadFenceModel>() != null)
                {
                    sampleSpacing = Mathf.Min(sampleSpacing, 0.4f);
                    break;
                }
            }
            List<SplineSamplingUtility.Sample> samples =
                SplineSamplingUtility.BuildArcLengthSamples(container, splineIndex, sampleSpacing);
            if (samples.Count < 2 || !IsFinite(samples[samples.Count - 1].Distance))
                return null;
            return samples;
        }

        public void ClearGenerated()
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing == null)
                return;

            foreach (RoadFenceGeneratedMesh owned in existing.GetComponentsInChildren<RoadFenceGeneratedMesh>(true))
                owned.Release();

            foreach (MeshFilter filter in existing.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh != null && mesh.name.StartsWith("Continuous Fence "))
                {
                    filter.sharedMesh = null;
                    if (Application.isPlaying)
                        Destroy(mesh);
                    else
                        DestroyImmediate(mesh);
                }
            }

            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        private void BuildLayer(
            Transform parent,
            IReadOnlyList<SplineSamplingUtility.Sample> samples,
            float length,
            RoadProfile profile,
            SplinePropLayer layer,
            int splineIndex)
        {
            if (!PropPlacementUtility.AppliesToSpline(layer, splineIndex) || layer.Prefab == null ||
                !IsFinite(length) || length <= 0f)
                return;

            var layerRoot = new GameObject($"Spline {splineIndex + 1} - {layer.Name}");
            layerRoot.transform.SetParent(parent, false);
            var random = new System.Random(layer.Seed);
            float exclusion = Mathf.Max(0f, FiniteOr(layer.IntersectionExclusionDistance, 0f));
            float start = Mathf.Max(0f, FiniteOr(layer.StartOffset, 0f), exclusion);
            float end = Mathf.Min(length, length - FiniteOr(layer.EndOffset, 0f), length - exclusion);

            RoadFenceModel model = layer.ContinuousFence ? layer.Prefab.GetComponent<RoadFenceModel>() : null;
            if (model != null)
            {
                if (end > start)
                    LastGeneratedInstanceCount += RoadFenceModelGenerator.Build(
                        layerRoot.transform, samples, profile, layer, model, start, end, splineIndex);
                return;
            }

            if (layer.ContinuousFence && end > start)
                RoadFenceMeshGenerator.Build(layerRoot.transform, samples, profile, layer, start, end, splineIndex);

            float spacing = Mathf.Max(0.1f, FiniteOr(layer.Spacing, 8f));
            for (float distance = start; distance <= end;)
            {
                SplineSamplingUtility.Sample sample = SplineSamplingUtility.EvaluateAtDistance(samples, distance);
                if (layer.Side == PropSide.Both)
                {
                    Spawn(layerRoot.transform, sample, profile, layer, -1f, random,
                        PropPlacementUtility.IsVisible(layer, splineIndex, -1f, distance));
                    Spawn(layerRoot.transform, sample, profile, layer, 1f, random,
                        PropPlacementUtility.IsVisible(layer, splineIndex, 1f, distance));
                }
                else
                {
                    float side = layer.Side == PropSide.Left ? -1f : layer.Side == PropSide.Right ? 1f : 0f;
                    Spawn(layerRoot.transform, sample, profile, layer, side, random,
                        PropPlacementUtility.IsVisible(layer, splineIndex, side, distance));
                }
                float next = distance + spacing;
                if (!IsFinite(next) || next <= distance) break;
                distance = next;
            }
        }

        private void Spawn(
            Transform parent,
            SplineSamplingUtility.Sample sample,
            RoadProfile profile,
            SplinePropLayer layer,
            float side,
            System.Random random,
            bool visible)
        {
            Vector3 right = Vector3.Cross(sample.Up, sample.Tangent).normalized;
            if (right.sqrMagnitude < 0.5f)
                right = Vector3.right;
            float lateral = side == 0f ? layer.LateralOffset : side * (profile.Width * 0.5f + layer.LateralOffset);
            Vector3 position = sample.Position + right * lateral + sample.Up * layer.VerticalOffset;
            if (profile.ConformRoadToTerrain || layer.Grounding == PropGroundingMode.Terrain)
                position = RoadTerrainHeightUtility.Conform(position, layer.VerticalOffset);
            Quaternion rotation = layer.FollowSplineRotation
                ? Quaternion.LookRotation(sample.Tangent, sample.Up)
                : Quaternion.identity;

            Vector3 randomEuler = new(
                NextSigned(random) * layer.RandomRotation.x,
                NextSigned(random) * layer.RandomRotation.y,
                NextSigned(random) * layer.RandomRotation.z);
            rotation *= Quaternion.Euler(layer.RotationOffset + randomEuler);
            float minimumScale = Mathf.Min(layer.RandomScale.x, layer.RandomScale.y);
            float maximumScale = Mathf.Max(layer.RandomScale.x, layer.RandomScale.y);
            float scale = Mathf.Lerp(minimumScale, maximumScale, (float)random.NextDouble());

            // Preserve the random stream for all remaining instances when an opening is edited.
            if (!visible)
                return;

            GameObject instance = Instantiate(layer.Prefab, position, rotation, parent);
            instance.name = layer.Prefab.name;
            instance.transform.localScale = layer.Prefab.transform.localScale * scale;
            if (layer.ContinuousFence)
            {
                // The rail is generated once as a curved mesh; keep only each prefab's post and bracket.
                foreach (string partName in RoadFenceMeshGenerator.RailPartNames)
                {
                    Transform part = instance.transform.Find(partName);
                    if (part != null)
                        part.gameObject.SetActive(false);
                }
            }
            LastGeneratedInstanceCount++;
        }

        private static float NextSigned(System.Random random) => (float)(random.NextDouble() * 2.0 - 1.0);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float FiniteOr(float value, float fallback) => IsFinite(value) ? value : fallback;
    }
}
