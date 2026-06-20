using ObjLoader.Plugin.CameraAnimation;

namespace ObjLoader.UnitTests.Infrastructure;

public sealed class LinearEasing : ICameraEasing
{
    public double GetEasedT(double t) => t;

    public List<BezierPointData> GetPoints() => new();

    public void SetPoints(List<BezierPointData> points) { }

    public bool IsQuadratic { get; set; }
}
