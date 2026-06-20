namespace ObjLoader.Plugin.CameraAnimation;

public interface ICameraEasing
{
    double GetEasedT(double t);
    List<BezierPointData> GetPoints();
    void SetPoints(List<BezierPointData> points);
    bool IsQuadratic { get; set; }
}
