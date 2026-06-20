using System.Collections.Immutable;
using System.Numerics;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Plugin.CameraAnimation;

public class BezierCameraEasing : ICameraEasing
{
    private readonly BezierAnimation _bezierAnimation = new();

    public BezierAnimation Animation => _bezierAnimation;

    public double GetEasedT(double t) => _bezierAnimation.GetAnimation(t);

    public List<BezierPointData> GetPoints() =>
        _bezierAnimation.Points.Select(p => new BezierPointData(p)).ToList();

    public void SetPoints(List<BezierPointData> points)
    {
        if (points == null || points.Count == 0) return;
        var pts = points.Select(p => p.ToAnimationPoint()).ToArray();
        _bezierAnimation.Points = ImmutableList.Create(new ReadOnlySpan<BezierAnimationPoint>(pts));
    }

    public bool IsQuadratic
    {
        get => _bezierAnimation.IsQuadratic;
        set => _bezierAnimation.IsQuadratic = value;
    }
}
