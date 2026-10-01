using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Dyma.SplineLevelToolkit.Editor
{
    [CustomEditor(typeof(RoadSocket))]
    public sealed class RoadSocketEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox(
                "Assign this socket in the Road Tools window, select a road, then use Snap Nearest Road Endpoint.",
                MessageType.Info);
        }
    }
}
