using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SplineRoad))]
    public sealed class RoadMaterialSections : MonoBehaviour
    {
        [SerializeField, HideInInspector] private List<RoadMaterialSection> sections = new();
        private bool refreshQueued;
        private bool hasSnapshot;
        private int layoutSnapshot;
        private SplineRoad owner;
        public IReadOnlyList<RoadMaterialSection> Sections => sections;

        private void OnEnable() => refreshQueued = true;
        private void OnValidate() => refreshQueued = true;
        private void OnDisable()
        {
            refreshQueued = false;
            Owner?.RefreshMaterial();
        }
        private void Update() { if (refreshQueued && !Application.isPlaying) Refresh(); }
        private SplineRoad Owner => owner != null ? owner : owner = GetComponent<SplineRoad>();

        public void Add(RoadMaterialSection section) { if (section != null) sections.Add(section); }
        public void RemoveAt(int index) { if (index >= 0 && index < sections.Count) sections.RemoveAt(index); }
        public Material GetMaterial(int index, Material fallback) => isActiveAndEnabled && index >= 0 && index < sections.Count &&
            sections[index] != null && sections[index].Enabled && sections[index].Material != null ? sections[index].Material : fallback;

        // Surface topology changes only when ranges change. Changing a material keeps
        // the road mesh, collider, terrain and props intact, including baked roads.
        public void Refresh()
        {
            refreshQueued = false;
            SplineRoad road = Owner;
            if (road == null) return;
            int layout = LayoutHash();
            bool changed = !hasSnapshot || layout != layoutSnapshot;
            hasSnapshot = true; layoutSnapshot = layout;
            if (changed && !road.IsBaked && isActiveAndEnabled) road.Rebuild(false);
            else road.RefreshMaterial();
        }

        public List<RoadMaterialSpan> Resolve(SplineContainer container, int splineIndex,
            IReadOnlyList<SplineSamplingUtility.Sample> samples)
        {
            var spans = new List<RoadMaterialSpan>();
            if (!isActiveAndEnabled || container == null || samples == null || samples.Count < 2 ||
                splineIndex < 0 || splineIndex >= container.Splines.Count) return spans;
            Spline spline = container.Splines[splineIndex];
            float length = samples[samples.Count - 1].Distance;
            for (int i = 0; i < sections.Count; i++)
            {
                RoadMaterialSection section = sections[i];
                if (section == null || !section.Enabled || section.Material == null || section.SplineIndex != splineIndex) continue;
                if (!TryGetRange(section, spline, samples, out float start, out float end)) continue;
                start = Mathf.Clamp(start, 0, length); end = Mathf.Clamp(end, 0, length);
                if (spline.Closed && end < start)
                {
                    if (length - start > 0.00001f) spans.Add(new RoadMaterialSpan(start, length, i));
                    if (end > 0.00001f) spans.Add(new RoadMaterialSpan(0, end, i));
                }
                else if (end - start > 0.00001f) spans.Add(new RoadMaterialSpan(start, end, i));
            }
            return spans;
        }

        public static bool TryGetRange(RoadMaterialSection section, Spline spline,
            IReadOnlyList<SplineSamplingUtility.Sample> samples, out float start, out float end)
        {
            start = end = 0;
            if (section == null || spline == null || samples == null || samples.Count < 2) return false;
            start = section.StartDistance; end = section.EndDistance;
            if (section.RangeMode == RoadMaterialRangeMode.Knots)
            {
                if (section.StartKnot < 0 || section.EndKnot < 0 || section.StartKnot >= spline.Count || section.EndKnot >= spline.Count) return false;
                start = KnotDistance(spline, section.StartKnot, samples);
                end = KnotDistance(spline, section.EndKnot, samples);
            }
            return !float.IsNaN(start) && !float.IsNaN(end) && !float.IsInfinity(start) && !float.IsInfinity(end);
        }

        private static float KnotDistance(Spline spline, int knot, IReadOnlyList<SplineSamplingUtility.Sample> samples)
        {
            float t = spline.ConvertIndexUnit(knot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            int low = 0, high = samples.Count - 1;
            while (high - low > 1) { int mid = (low + high) / 2; if (samples[mid].T < t) low = mid; else high = mid; }
            float interval = samples[high].T - samples[low].T;
            return Mathf.Lerp(samples[low].Distance, samples[high].Distance,
                interval > 0.000001f ? (t - samples[low].T) / interval : 0);
        }

        private int LayoutHash()
        {
            unchecked
            {
                int hash = sections.Count;
                foreach (RoadMaterialSection s in sections)
                {
                    if (s == null) { hash = hash * 31; continue; }
                    hash = hash * 31 + s.Enabled.GetHashCode(); hash = hash * 31 + (s.Material != null).GetHashCode();
                    hash = hash * 31 + s.SplineIndex; hash = hash * 31 + (int)s.RangeMode;
                    hash = hash * 31 + s.StartDistance.GetHashCode(); hash = hash * 31 + s.EndDistance.GetHashCode();
                    hash = hash * 31 + s.StartKnot; hash = hash * 31 + s.EndKnot;
                }
                return hash;
            }
        }
    }
}
