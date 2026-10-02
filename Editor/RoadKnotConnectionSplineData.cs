using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    // Embedded data follows each original point when the source flow is reversed and concatenated.
    internal static class RoadKnotConnectionSplineData
    {
        internal static void Copy(Spline source, Spline merged, bool reversed, int offset)
        {
            foreach (string key in source.GetIntDataKeys())
            {
                source.TryGetIntData(key, out var data);
                CopyPoints(source, data, merged.GetOrCreateIntData(key), reversed, offset);
            }
            foreach (string key in source.GetFloatDataKeys())
            {
                source.TryGetFloatData(key, out var data);
                CopyPoints(source, data, merged.GetOrCreateFloatData(key), reversed, offset);
            }
            foreach (string key in source.GetFloat4DataKeys())
            {
                source.TryGetFloat4Data(key, out var data);
                CopyPoints(source, data, merged.GetOrCreateFloat4Data(key), reversed, offset);
            }
            foreach (string key in source.GetObjectDataKeys())
            {
                source.TryGetObjectData(key, out var data);
                CopyPoints(source, data, merged.GetOrCreateObjectData(key), reversed, offset);
            }
        }

        private static void CopyPoints<T>(Spline source, SplineData<T> sourceData, SplineData<T> destination,
            bool reversed, int offset)
        {
            if (destination.Count == 0) destination.DefaultValue = sourceData.DefaultValue;
            destination.PathIndexUnit = PathIndexUnit.Knot;
            foreach (DataPoint<T> point in sourceData)
            {
                float knot = source.ConvertIndexUnit(point.Index, sourceData.PathIndexUnit, PathIndexUnit.Knot);
                destination.Add(offset + (reversed ? source.Count - 1f - knot : knot), point.Value);
            }
        }
    }
}
