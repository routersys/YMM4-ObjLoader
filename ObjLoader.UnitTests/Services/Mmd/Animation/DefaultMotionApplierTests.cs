using ObjLoader.Services.Mmd.Animation;
using ObjLoader.Services.Mmd.Animation.Interfaces;
using ObjLoader.Services.Mmd.Parsers;
using ObjLoader.UnitTests.Infrastructure;
using System.Numerics;

namespace ObjLoader.UnitTests.Services.Mmd.Animation;

public class DefaultMotionApplierTests
{
    private readonly IMotionApplier _applier = new DefaultMotionApplier();

    [Fact]
    public void ConvertCameraFrames_EmptyCameraFrames_ReturnsEmptyList()
    {
        var vmdData = new VmdData { CameraFrames = new List<VmdCameraFrame>() };

        var result = _applier.ConvertCameraFrames(vmdData, Vector3.Zero, 1.0f);

        Assert.Empty(result);
    }

    [Fact]
    public void ConvertCameraFrames_WithCameraFrames_ReturnsCameraKeyframes()
    {
        StaTestHelper.RunInSTA(() =>
        {
            var vmdData = new VmdData
            {
                CameraFrames = new List<VmdCameraFrame>
                {
                    new()
                    {
                        FrameNumber = 30,
                        Distance = 100,
                        Position = new Vector3(10, 20, 30),
                        Rotation = new Vector3(0, 0, 0)
                    }
                }
            };

            var result = _applier.ConvertCameraFrames(vmdData, Vector3.Zero, 1.0f);

            Assert.Single(result);
            Assert.Equal(1.0, result[0].Time);
            Assert.NotNull(result[0].Easing);
        });
    }

    [Fact]
    public void ConvertCameraFrames_SortsFramesByFrameNumber()
    {
        StaTestHelper.RunInSTA(() =>
        {
            var vmdData = new VmdData
            {
                CameraFrames = new List<VmdCameraFrame>
                {
                    new() { FrameNumber = 60, Distance = 0, Position = Vector3.Zero, Rotation = Vector3.Zero },
                    new() { FrameNumber = 30, Distance = 0, Position = Vector3.Zero, Rotation = Vector3.Zero }
                }
            };

            var result = _applier.ConvertCameraFrames(vmdData, Vector3.Zero, 1.0f);

            Assert.Equal(2, result.Count);
            Assert.True(result[0].Time < result[1].Time);
        });
    }

    [Fact]
    public void GetDuration_ReturnsDurationInSeconds()
    {
        var vmdData = new VmdData
        {
            CameraFrames = new List<VmdCameraFrame>
            {
                new() { FrameNumber = 90, Distance = 0, Position = Vector3.Zero, Rotation = Vector3.Zero }
            },
            BoneFrames = new List<VmdBoneFrame>(),
            MorphFrames = new List<VmdMorphFrame>()
        };

        var duration = _applier.GetDuration(vmdData);

        Assert.Equal(3.0, duration);
    }
}
