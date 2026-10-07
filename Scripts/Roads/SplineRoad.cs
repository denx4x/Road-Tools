using System;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplineContainer), typeof(RoadMeshGenerator), typeof(PropLayerManager))]
    [RequireComponent(typeof(RoadColliderGenerator))]
    public sealed class SplineRoad : MonoBehaviour
    {
        [SerializeField] private RoadProfile profile;
        [SerializeField, HideInInspector] private Material materialOverride;
        [SerializeField] private bool rebuildAutomatically = true;
        [SerializeField, HideInInspector] private bool baked;
        [SerializeField, HideInInspector] private bool adjustTerrainToRoad;

        private SplineContainer splineContainer;
        private RoadMeshGenerator meshGenerator;
        private RoadColliderGenerator colliderGenerator;
        private PropLayerManager propLayerManager;
        private bool rebuildQueued;
        private Material validatedMaterialOverride;
        private RoadProfile validatedProfile;
        private bool validatedLiveUpdates;
        private bool validatedBaked;
        private bool validatedTerrainAdjustment;
        private bool hasValidatedState;

        public RoadProfile Profile => profile;
        public Material MaterialOverride => materialOverride;
        public Material EffectiveMaterial => materialOverride != null ? materialOverride : profile != null ? profile.Material : null;
        public bool IsBaked => baked;
        public bool AdjustTerrainToRoad => adjustTerrainToRoad;
        public bool LiveUpdatesEnabled => rebuildAutomatically;
        public bool HasPendingRebuild => rebuildQueued;
        // Editor coordination is optional; runtime assemblies do not depend on UnityEditor.
        public static event Action<SplineRoad> AutoRebuildRequested;
        public RoadBuildReport LastBuildReport { get; private set; }

        private void OnEnable()
        {
            // Unity reenables recorded components when undoing a material assignment.
            bool restoringMaterial = AuthoringStateMatchesSnapshot() &&
                materialOverride != validatedMaterialOverride && transform.Find("Generated Road Mesh") != null;
            CacheComponents();
            RememberValidatedState();
            Spline.Changed += OnSplineChanged;
            SplineContainer.SplineAdded += OnSplineCollectionChanged;
            SplineContainer.SplineRemoved += OnSplineCollectionChanged;
            if (restoringMaterial)
                ApplyMaterial();
            else if (!Application.isPlaying && !baked)
                RequestRebuild();
        }

        private void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
            SplineContainer.SplineAdded -= OnSplineCollectionChanged;
            SplineContainer.SplineRemoved -= OnSplineCollectionChanged;
        }

        private void OnValidate()
        {
            CacheComponents();
            // Unity may validate repeatedly during Undo. Revalidate appearance without
            // rebuilding when the road's geometry/terrain authoring state is unchanged.
            bool authoringStateUnchanged = AuthoringStateMatchesSnapshot();
            bool materialChanged = materialOverride != validatedMaterialOverride;
            RememberValidatedState();
            if (authoringStateUnchanged)
            {
                if (materialChanged) ApplyMaterial();
                return;
            }
            if (isActiveAndEnabled && rebuildAutomatically && !baked)
                RequestRebuild();
        }

        private void Update()
        {
            if (!Application.isPlaying && rebuildQueued && rebuildAutomatically && !baked && AutoRebuildRequested == null)
                Rebuild();
        }

        public void SetLiveUpdates(bool enabled)
        {
            rebuildAutomatically = enabled;
            RememberValidatedState();
            if (enabled) RequestRebuild();
            else rebuildQueued = false;
        }

        public void RequestRebuild()
        {
            if (Application.isPlaying || !isActiveAndEnabled || !rebuildAutomatically || baked) return;
            rebuildQueued = true;
            AutoRebuildRequested?.Invoke(this);
        }

        public void SetProfile(RoadProfile value)
        {
            profile = value;
            baked = false;
            RememberValidatedState();
            Rebuild();
        }

        public void SetMaterialOverride(Material value)
        {
            materialOverride = value;
            RememberValidatedState();
            ApplyMaterial();
        }

        private void ApplyMaterial()
        {
            Transform root = transform.Find("Generated Road Mesh");
            if (root == null) return;
            RoadMaterialSections sections = GetComponent<RoadMaterialSections>();
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.TryGetComponent(out RoadMaterialChunk chunk)) chunk.Apply(EffectiveMaterial, sections);
                else renderer.sharedMaterial = EffectiveMaterial;
            }
        }

        public void RefreshMaterial() => ApplyMaterial();

        private void RememberValidatedState()
        {
            validatedMaterialOverride = materialOverride;
            validatedProfile = profile;
            validatedLiveUpdates = rebuildAutomatically;
            validatedBaked = baked;
            validatedTerrainAdjustment = adjustTerrainToRoad;
            hasValidatedState = true;
        }

        private bool AuthoringStateMatchesSnapshot() => hasValidatedState &&
            profile == validatedProfile && rebuildAutomatically == validatedLiveUpdates &&
            baked == validatedBaked && adjustTerrainToRoad == validatedTerrainAdjustment;

        public RoadBuildReport Rebuild() => Rebuild(true);

        public RoadBuildReport Rebuild(bool rebuildProps)
        {
            rebuildQueued = false;
            CacheComponents();
            if (baked)
                return LastBuildReport = new RoadBuildReport(false, "Road is baked. Resume editing before rebuilding.");
            if (profile == null)
                return LastBuildReport = new RoadBuildReport(false, "Assign a Road Profile before rebuilding.");
            if (splineContainer == null || splineContainer.Splines.Count == 0)
                return LastBuildReport = new RoadBuildReport(false, "Add a spline with at least two knots.");

            meshGenerator.Rebuild(splineContainer, profile, materialOverride, GetComponent<RoadMaterialSections>());
            if (colliderGenerator == null)
                colliderGenerator = gameObject.AddComponent<RoadColliderGenerator>();
            colliderGenerator.Rebuild(transform.Find("Generated Road Mesh"), profile);
            if (rebuildProps) propLayerManager.Rebuild(splineContainer, profile, meshGenerator.LastJunctionLayout);
            if (meshGenerator.LastGeneratedMeshCount == 0)
                return LastBuildReport = new RoadBuildReport(false, "No road mesh was generated. Check the spline knots.");

            return LastBuildReport = CurrentBuildReport();
        }

        private RoadBuildReport CurrentBuildReport()
        {
            return new RoadBuildReport(
                true,
                $"Built {meshGenerator.LastGeneratedMeshCount} mesh chunks, " +
                $"{meshGenerator.LastGeneratedVertexCount} vertices, {colliderGenerator.LastGeneratedColliderCount} colliders, and " +
                $"{propLayerManager.LastGeneratedInstanceCount} props. " +
                $"Narrowed {meshGenerator.LastSelfOverlapReductionCount} samples near overlapping road sections.",
                meshGenerator.LastGeneratedMeshCount,
                meshGenerator.LastGeneratedVertexCount,
                propLayerManager.LastGeneratedInstanceCount,
                meshGenerator.LastSelfOverlapReductionCount,
                colliderGenerator.LastGeneratedColliderCount);
        }

        public void MarkBaked()
        {
            baked = true;
            RememberValidatedState();
        }

        public void RebuildProps()
        {
            CacheComponents();
            if (!baked && propLayerManager != null && splineContainer != null && profile != null)
            {
                propLayerManager.Rebuild(splineContainer, profile);
                if (LastBuildReport.Succeeded) LastBuildReport = CurrentBuildReport();
            }
        }

        public void SetTerrainAdjustment(bool enabled)
        {
            adjustTerrainToRoad = enabled;
            RememberValidatedState();
            RequestRebuild();
        }

        public RoadBuildReport ResumeEditing()
        {
            baked = false;
            RememberValidatedState();
            return Rebuild();
        }

        [ContextMenu("Rebuild Road")]
        private void RebuildFromContextMenu()
        {
            Rebuild();
        }

        private void CacheComponents()
        {
            if (splineContainer == null)
                splineContainer = GetComponent<SplineContainer>();
            if (meshGenerator == null)
                meshGenerator = GetComponent<RoadMeshGenerator>();
            if (colliderGenerator == null)
                colliderGenerator = GetComponent<RoadColliderGenerator>();
            if (propLayerManager == null)
                propLayerManager = GetComponent<PropLayerManager>();
        }

        private void OnSplineChanged(Spline changedSpline, int knotIndex, SplineModification modification)
        {
            if (!rebuildAutomatically || baked || splineContainer == null)
                return;

            bool belongsToContainer = false;
            for (int i = 0; i < splineContainer.Splines.Count; i++)
            {
                if (splineContainer.Splines[i] != changedSpline)
                    continue;

                belongsToContainer = true;
                break;
            }

            if (!belongsToContainer)
                return;

            RequestRebuild();
        }

        private void OnSplineCollectionChanged(SplineContainer changedContainer, int splineIndex)
        {
            if (!rebuildAutomatically || baked || changedContainer != splineContainer)
                return;

            RequestRebuild();
        }
    }
}
