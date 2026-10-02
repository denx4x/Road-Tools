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

        public IReadOnlyList<SplinePropLayer> Layers => layers;
        public int LastGeneratedInstanceCount { get; private set; }

        public void AddLayer() => layers.Add(new SplinePropLayer());

        public void RemoveLayer(int index)
        {
            if (index >= 0 && index < layers.Count)
                layers.RemoveAt(index);
        }

        public float GetRoadShoulderRadius(RoadProfile profile)
        {
            float baseHalfWidth = profile != null ? profile.Width * 0.5f : 0f;
            float radius = baseHalfWidth;
            foreach (SplinePropLayer layer in layers)
            {
                if (layer == null || layer.Prefab == null || layer.Grounding != PropGroundingMode.RoadShoulder)
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
            if (spline == null || profile == null || spline.Splines.Count == 0)
                return;
            var root = new GameObject(GeneratedRootName);
            root.transform.SetParent(transform, false);

            for (int splineIndex = 0; splineIndex < spline.Splines.Count; splineIndex++)
            {
                if (spline.Splines[splineIndex] == null || spline.Splines[splineIndex].Count < 2)
                    continue;

                float sampleSpacing = Mathf.Max(0.5f, profile.SampleSpacing);
                foreach (SplinePropLayer layer in layers)
                {
                    if (layer != null && layer.ContinuousFence && layer.Prefab != null &&
                        layer.Prefab.GetComponent<RoadFenceModel>() != null)
                    {
                        sampleSpacing = Mathf.Min(sampleSpacing, 0.4f);
                        break;
                    }
                }
                List<SplineSamplingUtility.Sample> samples =
                    SplineSamplingUtility.BuildArcLengthSamples(spline, splineIndex, sampleSpacing);
                if (samples.Count < 2)
                    continue;

                float length = samples[samples.Count - 1].Distance;
                foreach (SplinePropLayer layer in layers)
                    BuildLayer(root.transform, samples, length, profile, layer, splineIndex);
            }
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
            if (layer == null || layer.Prefab == null)
                return;

            var layerRoot = new GameObject($"Spline {splineIndex + 1} - {layer.Name}");
            layerRoot.transform.SetParent(parent, false);
            var random = new System.Random(layer.Seed);
            float start = Mathf.Max(layer.StartOffset, layer.IntersectionExclusionDistance);
            float end = Mathf.Min(length - layer.EndOffset, length - layer.IntersectionExclusionDistance);

            RoadFenceModel model = layer.ContinuousFence ? layer.Prefab.GetComponent<RoadFenceModel>() : null;
            if (model != null)
            {
                if (end > start)
                    LastGeneratedInstanceCount += RoadFenceModelGenerator.Build(
                        layerRoot.transform, samples, profile, layer, model, start, end);
                return;
            }

            if (layer.ContinuousFence && end > start)
                RoadFenceMeshGenerator.Build(layerRoot.transform, samples, profile, layer, start, end);

            for (float distance = start; distance <= end; distance += Mathf.Max(0.1f, layer.Spacing))
            {
                SplineSamplingUtility.Sample sample = SplineSamplingUtility.EvaluateAtDistance(samples, distance);
                if (layer.Side == PropSide.Both)
                {
                    Spawn(layerRoot.transform, sample, profile, layer, -1f, random);
                    Spawn(layerRoot.transform, sample, profile, layer, 1f, random);
                }
                else
                {
                    float side = layer.Side == PropSide.Left ? -1f : layer.Side == PropSide.Right ? 1f : 0f;
                    Spawn(layerRoot.transform, sample, profile, layer, side, random);
                }
            }
        }

        private void Spawn(
            Transform parent,
            SplineSamplingUtility.Sample sample,
            RoadProfile profile,
            SplinePropLayer layer,
            float side,
            System.Random random)
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
    }
}
