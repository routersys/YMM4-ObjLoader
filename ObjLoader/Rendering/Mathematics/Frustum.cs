using System.Numerics;

namespace ObjLoader.Rendering.Mathematics;

public struct Frustum
{
    private Plane _plane0;
    private Plane _plane1;
    private Plane _plane2;
    private Plane _plane3;
    private Plane _plane4;
    private Plane _plane5;

    public Frustum(Matrix4x4 viewProjection)
    {
        ExtractPlanes(viewProjection);
    }

    private void ExtractPlanes(Matrix4x4 vp)
    {
        _plane0 = NormalizePlane(new Plane(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41));
        _plane1 = NormalizePlane(new Plane(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41));
        _plane2 = NormalizePlane(new Plane(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42));
        _plane3 = NormalizePlane(new Plane(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42));
        _plane4 = NormalizePlane(new Plane(vp.M13, vp.M23, vp.M33, vp.M43));
        _plane5 = NormalizePlane(new Plane(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43));
    }

    private static Plane NormalizePlane(Plane p)
    {
        float length = p.Normal.Length();
        if (length > 0.0001f)
        {
            p.Normal /= length;
            p.D /= length;
        }
        return p;
    }

    public readonly bool Intersects(CullingBox box)
    {
        if (box.Min.X > box.Max.X) return false;

        if (!TestPlane(_plane0, box)) return false;
        if (!TestPlane(_plane1, box)) return false;
        if (!TestPlane(_plane2, box)) return false;
        if (!TestPlane(_plane3, box)) return false;
        if (!TestPlane(_plane4, box)) return false;
        if (!TestPlane(_plane5, box)) return false;

        return true;
    }

    private static bool TestPlane(Plane plane, CullingBox box)
    {
        Vector3 p = new Vector3(
            plane.Normal.X > 0 ? box.Max.X : box.Min.X,
            plane.Normal.Y > 0 ? box.Max.Y : box.Min.Y,
            plane.Normal.Z > 0 ? box.Max.Z : box.Min.Z);

        return Vector3.Dot(plane.Normal, p) + plane.D >= 0;
    }
}