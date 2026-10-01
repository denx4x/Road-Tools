namespace Dyma.SplineLevelToolkit
{
    public readonly struct RoadBuildReport
    {
        public RoadBuildReport(bool succeeded, string message, int meshCount = 0,
            int vertexCount = 0, int propCount = 0, int narrowedSamples = 0, int colliderCount = 0)
        {
            Succeeded = succeeded;
            Message = message;
            MeshCount = meshCount;
            VertexCount = vertexCount;
            PropCount = propCount;
            NarrowedSamples = narrowedSamples;
            ColliderCount = colliderCount;
        }

        public bool Succeeded { get; }
        public string Message { get; }
        public int MeshCount { get; }
        public int VertexCount { get; }
        public int PropCount { get; }
        public int NarrowedSamples { get; }
        public int ColliderCount { get; }
    }
}
