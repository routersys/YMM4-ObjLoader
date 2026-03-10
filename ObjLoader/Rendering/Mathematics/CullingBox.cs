using System.Numerics;

namespace ObjLoader.Rendering.Mathematics;

public struct CullingBox
{
    public Vector3 Min;
    public Vector3 Max;

    public CullingBox()
    {
        Min = new Vector3(float.MaxValue);
        Max = new Vector3(-float.MaxValue);
    }

    public CullingBox(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public void Expand(Vector3 point)
    {
        Min = Vector3.Min(Min, point);
        Max = Vector3.Max(Max, point);
    }

    public static CullingBox Transform(CullingBox box, Matrix4x4 matrix)
    {
        if (box.Min.X > box.Max.X) return box;

        Span<Vector3> corners = stackalloc Vector3[8]
        {
            new Vector3(box.Min.X, box.Min.Y, box.Min.Z),
            new Vector3(box.Max.X, box.Min.Y, box.Min.Z),
            new Vector3(box.Min.X, box.Max.Y, box.Min.Z),
            new Vector3(box.Max.X, box.Max.Y, box.Min.Z),
            new Vector3(box.Min.X, box.Min.Y, box.Max.Z),
            new Vector3(box.Max.X, box.Min.Y, box.Max.Z),
            new Vector3(box.Min.X, box.Max.Y, box.Max.Z),
            new Vector3(box.Max.X, box.Max.Y, box.Max.Z)
        };

        Vector3 newMin = new Vector3(float.MaxValue);
        Vector3 newMax = new Vector3(-float.MaxValue);

        for (int i = 0; i < 8; i++)
        {
            Vector3 transformed = Vector3.Transform(corners[i], matrix);
            newMin = Vector3.Min(newMin, transformed);
            newMax = Vector3.Max(newMax, transformed);
        }

        return new CullingBox(newMin, newMax);
    }
}