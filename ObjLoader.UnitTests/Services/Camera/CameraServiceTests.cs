using ObjLoader.Plugin.CameraAnimation;
using ObjLoader.Services.Camera;
using ObjLoader.UnitTests.Infrastructure;

namespace ObjLoader.UnitTests.Services.Camera;

public class CameraServiceTests
{
    private static CameraKeyframe Keyframe(double time, double camX = 0, double camY = 0, double camZ = 0,
        double targetX = 0, double targetY = 0, double targetZ = 0) =>
        new()
        {
            Easing = new LinearEasing(),
            Time = time,
            CamX = camX,
            CamY = camY,
            CamZ = camZ,
            TargetX = targetX,
            TargetY = targetY,
            TargetZ = targetZ
        };

    private readonly ICameraService _cameraService = new CameraService();

    [Fact]
    public void CalculateCameraState_EmptyKeyframes_ReturnsZeros()
    {
        var result = _cameraService.CalculateCameraState(new List<CameraKeyframe>(), 1.0);

        Assert.Equal((0.0, 0.0, 0.0, 0.0, 0.0, 0.0), result);
    }

    [Fact]
    public void CalculateCameraState_SingleKeyframe_ReturnsKeyframeState()
    {
        var keyframes = new List<CameraKeyframe> { Keyframe(1.0, camX: 10, targetX: 20) };

        var result = _cameraService.CalculateCameraState(keyframes, 2.0);

        Assert.Equal(10.0, result.cx);
        Assert.Equal(20.0, result.tx);
    }

    [Fact]
    public void CalculateCameraState_BetweenKeyframes_InterpolatesLinearly()
    {
        var keyframes = new List<CameraKeyframe>
        {
            Keyframe(1.0, camX: 0, targetY: 0),
            Keyframe(3.0, camX: 10, targetY: 20)
        };

        var result = _cameraService.CalculateCameraState(keyframes, 2.0);

        Assert.Equal(5.0, result.cx, 3);
        Assert.Equal(10.0, result.ty, 3);
    }

    [Fact]
    public void CalculateCameraState_BeforeFirstKeyframe_ReturnsFirstKeyframe()
    {
        var keyframes = new List<CameraKeyframe>
        {
            Keyframe(2.0, camX: 10),
            Keyframe(4.0, camX: 20)
        };

        var result = _cameraService.CalculateCameraState(keyframes, 1.0);

        Assert.Equal(10.0, result.cx);
    }

    [Fact]
    public void CalculateCameraState_AfterLastKeyframe_ReturnsLastKeyframe()
    {
        var keyframes = new List<CameraKeyframe>
        {
            Keyframe(2.0, camX: 10),
            Keyframe(4.0, camX: 20)
        };

        var result = _cameraService.CalculateCameraState(keyframes, 5.0);

        Assert.Equal(20.0, result.cx);
    }
}
