using System;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit
{
    /// <summary>An independent, editable prop route that refreshes only its owning road's props.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplineContainer))]
    public sealed class RoadPropPath : MonoBehaviour
    {
        private const double RefreshInterval = 0.08;
        [SerializeField] private SplineRoad owner;

        private SplineContainer container;
        private PropLayerManager layers;
        private SplineRoad cachedOwner;
        private Matrix4x4 watchedMatrix;
        private int watchedSplineState;
        private bool validationQueued;
        private bool rebuildQueued;
        private double nextRefresh;

        public SplineRoad Owner => owner;
        public bool HasPendingRebuild => rebuildQueued || validationQueued;
        // Optional Editor scheduling also supplies updates after the last mouse-drag event.
        public static event Action<RoadPropPath> AutoRebuildRequested;
        public static event Action<SplineRoad> OwnerPropsInvalidated;

        private void OnEnable()
        {
            CacheComponents();
            RememberState();
            validationQueued = true;
            Spline.Changed += OnSplineChanged;
            SplineContainer.SplineAdded += OnSplineCollectionChanged;
            SplineContainer.SplineRemoved += OnSplineCollectionChanged;
            RequestRebuild();
        }

        private void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
            SplineContainer.SplineAdded -= OnSplineCollectionChanged;
            SplineContainer.SplineRemoved -= OnSplineCollectionChanged;
            rebuildQueued = false;
            validationQueued = false;
        }

        private void OnDestroy()
        {
            if (Application.isPlaying || !CanEditOwner()) return;
            // Rebuild after destruction so a missing path cannot leave stale fence geometry.
            if (OwnerPropsInvalidated != null) OwnerPropsInvalidated.Invoke(owner);
            else owner.RequestRebuild();
        }

        // Validation can run during deserialization; generation is deferred to the main thread.
        private void OnValidate() => validationQueued = true;

        private void Update()
        {
            if (Application.isPlaying) return;
            ObserveChanges();
            if (AutoRebuildRequested == null && Time.realtimeSinceStartupAsDouble >= nextRefresh)
                FlushPending();
        }

        public void Configure(SplineRoad value)
        {
            owner = value;
            CacheComponents();
            RememberState();
            RequestRebuild();
        }

        public void RequestRebuild()
        {
            if (Application.isPlaying || !isActiveAndEnabled || !CanEditOwner()) return;
            if (!rebuildQueued) nextRefresh = Time.realtimeSinceStartupAsDouble + RefreshInterval;
            rebuildQueued = true;
            AutoRebuildRequested?.Invoke(this);
        }

        public bool FlushPending()
        {
            if (Application.isPlaying || !isActiveAndEnabled) return false;
            ObserveChanges();
            if (!rebuildQueued) return false;
            rebuildQueued = false;
            if (!CanEditOwner() || !IsAssignedToOwner()) return false;
            owner.RebuildProps();
            return true;
        }

        private void ObserveChanges()
        {
            CacheComponents();
            Matrix4x4 matrix = transform.localToWorldMatrix;
            int splineState = SplineState();
            if (!validationQueued && matrix == watchedMatrix && splineState == watchedSplineState) return;
            validationQueued = false;
            watchedMatrix = matrix;
            watchedSplineState = splineState;
            RequestRebuild();
        }

        private void CacheComponents()
        {
            if (container == null) container = GetComponent<SplineContainer>();
            if (cachedOwner == owner && (owner == null || layers != null)) return;
            cachedOwner = owner;
            layers = owner != null ? owner.GetComponent<PropLayerManager>() : null;
        }

        private bool CanEditOwner() => owner != null && owner.isActiveAndEnabled &&
            owner.LiveUpdatesEnabled && !owner.IsBaked && owner.Profile != null &&
            owner.gameObject.scene == gameObject.scene;

        private bool IsAssignedToOwner()
        {
            if (container == null || layers == null) return false;
            foreach (SplinePropLayer layer in layers.Layers)
            {
                if (layer == null || !layer.Enabled || layer.CustomPath != container) continue;
                int index = layer.CustomSplineIndex;
                if (index >= 0 && index < container.Splines.Count &&
                    container.Splines[index] != null && container.Splines[index].Count >= 2)
                    return true;
            }
            // A duplicated or unassigned path must never silently rebuild another road.
            return false;
        }

        private void RememberState()
        {
            watchedMatrix = transform.localToWorldMatrix;
            watchedSplineState = SplineState();
        }

        private int SplineState()
        {
            if (container == null) return 0;
            // Undo can restore serialized knots without publishing Spline.Changed. This
            // small allocation-free signature observes only this path's authored knots.
            unchecked
            {
                int hash = container.Splines.Count;
                for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
                {
                    Spline spline = container.Splines[splineIndex];
                    if (spline == null) { hash *= 31; continue; }
                    hash = hash * 31 + spline.GetHashCode();
                    hash = hash * 31 + spline.Count;
                    hash = hash * 31 + spline.Closed.GetHashCode();
                    for (int i = 0; i < spline.Count; i++)
                    {
                        BezierKnot knot = spline[i];
                        hash = hash * 31 + knot.Position.GetHashCode();
                        hash = hash * 31 + knot.Rotation.GetHashCode();
                        hash = hash * 31 + knot.TangentIn.GetHashCode();
                        hash = hash * 31 + knot.TangentOut.GetHashCode();
                        hash = hash * 31 + (int)spline.GetTangentMode(i);
                    }
                }
                return hash;
            }
        }

        private void OnSplineChanged(Spline changedSpline, int knotIndex, SplineModification modification)
        {
            if (container == null) return;
            foreach (Spline spline in container.Splines)
                if (spline == changedSpline) { RequestRebuild(); return; }
        }

        private void OnSplineCollectionChanged(SplineContainer changedContainer, int splineIndex)
        {
            if (changedContainer == container) RequestRebuild();
        }
    }
}
