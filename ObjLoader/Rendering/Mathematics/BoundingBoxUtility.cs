using ObjLoader.Core.Models;

namespace ObjLoader.Rendering.Mathematics;

public static class BoundingBoxUtility
{
    public static (CullingBox GlobalBox, ModelPart[] Parts) CalculateBounds(ObjModel model)
    {
        var globalBox = new CullingBox();
        var parts = model.Parts.ToArray();

        for (int i = 0; i < parts.Length; i++)
        {
            var partBox = new CullingBox();
            int endIndex = parts[i].IndexOffset + parts[i].IndexCount;

            for (int j = parts[i].IndexOffset; j < endIndex; j++)
            {
                var vertex = model.Vertices[model.Indices[j]];
                partBox.Expand(vertex.Position);
            }

            parts[i].LocalBoundingBox = partBox;
            globalBox.Expand(partBox.Min);
            globalBox.Expand(partBox.Max);
        }

        return (globalBox, parts);
    }
}