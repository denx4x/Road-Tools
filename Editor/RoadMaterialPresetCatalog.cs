using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadMaterialPresetCatalog
    {
        internal static readonly string[] Labels =
        {
            "Profile Default", "Fully Marked", "Left Edge + Center", "Right Edge + Center",
            "Edge Lines Only", "Unmarked Asphalt"
        };

        private static readonly string[] Assets =
        {
            null, "Road Asphalt Marked", "Road Asphalt Left Edge", "Road Asphalt Right Edge",
            "Road Asphalt Edges Only", "Road Asphalt Unmarked"
        };

        internal static Material Load(int index) => index > 0 && index < Assets.Length
            ? RoadToolsPackagePaths.LoadDefault<Material>("Materials/" + Assets[index] + ".mat") : null;

        internal static int Find(Material material)
        {
            if (material == null) return 0;
            for (int i = 1; i < Assets.Length; i++)
                if (Load(i) == material) return i;
            return -1;
        }
    }
}
