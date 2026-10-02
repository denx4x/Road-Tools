using System.Collections.Generic;
using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    [CreateAssetMenu(menuName = "Road Tools/Prop Preset", fileName = "Road Prop Preset")]
    public sealed class RoadPropPreset : ScriptableObject
    {
        [SerializeField, TextArea(1, 3)] private string description;
        [SerializeField] private Texture2D thumbnail;
        [SerializeField] private List<SplinePropLayer> layers = new();

        public string Description => description;
        public Texture2D Thumbnail => thumbnail;
        public IReadOnlyList<SplinePropLayer> Layers => layers;
    }
}
