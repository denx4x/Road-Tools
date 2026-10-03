using UnityEditor;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    [CustomEditor(typeof(RoadMaterialSections))]
    public sealed class RoadMaterialSectionsEditor : UnityEditor.Editor
    {
        private readonly RoadMaterialSectionsPanel panel = new RoadMaterialSectionsPanel();
        public override void OnInspectorGUI()
        {
            panel.Draw(((RoadMaterialSections)target).GetComponent<SplineRoad>());
            if (GUILayout.Button("Open Road Tools")) SplineLevelToolkitWindow.Open();
        }
    }
}
