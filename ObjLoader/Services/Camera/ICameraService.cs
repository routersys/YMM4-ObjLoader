using ObjLoader.Plugin.CameraAnimation;

namespace ObjLoader.Services.Camera;

public interface ICameraService
{
    (double cx, double cy, double cz, double tx, double ty, double tz) CalculateCameraState(
        IReadOnlyList<CameraKeyframe> keyframes, double time);
}
