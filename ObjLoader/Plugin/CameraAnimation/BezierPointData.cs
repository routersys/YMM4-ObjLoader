using System.Numerics;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Plugin.CameraAnimation;

public class BezierPointData
{
    public float PX { get; set; }
    public float PY { get; set; }
    public float C1X { get; set; }
    public float C1Y { get; set; }
    public float C2X { get; set; }
    public float C2Y { get; set; }

    public BezierPointData() { }

    public BezierPointData(BezierAnimationPoint p)
    {
        PX = p.Point.X; PY = p.Point.Y;
        C1X = p.ControlPoint1.X; C1Y = p.ControlPoint1.Y;
        C2X = p.ControlPoint2.X; C2Y = p.ControlPoint2.Y;
    }

    public BezierAnimationPoint ToAnimationPoint() =>
        new(new Vector2(PX, PY), new Vector2(C1X, C1Y), new Vector2(C2X, C2Y));
}
