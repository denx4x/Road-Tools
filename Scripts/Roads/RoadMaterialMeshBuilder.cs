using System.Collections.Generic;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    internal static class RoadMaterialMeshBuilder
    {
        internal static List<float> RowDistances(float start, float end, float spacing, IReadOnlyList<RoadMaterialSpan> spans)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt((end - start) / spacing) + 1);
            var distances = new List<float>(count + spans.Count * 2);
            for (int i = 0; i < count; i++) distances.Add(Mathf.Lerp(start, end, i / (float)(count - 1)));
            foreach (RoadMaterialSpan span in spans)
            {
                if (span.Start > start && span.Start < end) distances.Add(span.Start);
                if (span.End > start && span.End < end) distances.Add(span.End);
            }
            distances.Sort();
            for (int i = distances.Count - 1; i > 0; i--)
                if (distances[i] - distances[i-1] < 0.00001f) distances.RemoveAt(i);
            return distances;
        }

        internal static int[] ApplyTriangles(Mesh mesh, int[] triangles, IReadOnlyList<float> distances,
            IReadOnlyList<RoadMaterialSpan> spans)
        {
            var slots = new List<int>();
            var indices = new List<List<int>>();
            for (int row = 0; row < distances.Count - 1; row++)
            {
                int slot = SlotAt((distances[row] + distances[row+1]) * 0.5f, spans, slots, indices);
                for (int i = 0; i < 24; i++) indices[slot].Add(triangles[row*24+i]);
            }
            int caps = triangles.Length - 12;
            int first = SlotAt((distances[0] + distances[1]) * 0.5f, spans, slots, indices);
            int last = SlotAt((distances[distances.Count-2] + distances[distances.Count-1]) * 0.5f, spans, slots, indices);
            for (int i = 0; i < 6; i++) { indices[first].Add(triangles[caps+i]); indices[last].Add(triangles[caps+6+i]); }
            mesh.subMeshCount = slots.Count;
            for (int i = 0; i < slots.Count; i++) mesh.SetTriangles(indices[i], i);
            return slots.ToArray();
        }

        private static int SlotAt(float distance, IReadOnlyList<RoadMaterialSpan> spans,
            List<int> slots, List<List<int>> indices)
        {
            int section = -1;
            // Later entries win overlapping ranges, making precedence deterministic.
            foreach (RoadMaterialSpan span in spans)
                if (distance >= span.Start && distance < span.End) section = span.SectionIndex;
            int slot = slots.IndexOf(section);
            if (slot >= 0) return slot;
            slots.Add(section); indices.Add(new List<int>()); return slots.Count-1;
        }
    }
}
