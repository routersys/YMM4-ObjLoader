using ObjLoader.Plugin.CameraAnimation;
using ObjLoader.UnitTests.Infrastructure;

namespace ObjLoader.UnitTests.Plugin.CameraAnimation;

public class CameraKeyframeTests
{
    [Fact]
    public void PropertyChanges_TriggerPropertyChangedEvent()
    {
        var keyframe = new CameraKeyframe { Easing = new LinearEasing() };
        var changedProperties = new List<string>();
        keyframe.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null) changedProperties.Add(e.PropertyName);
        };

        keyframe.Time = 1.0;
        keyframe.CamX = 10.0;
        keyframe.CamY = 20.0;
        keyframe.CamZ = 30.0;
        keyframe.TargetX = 40.0;
        keyframe.TargetY = 50.0;
        keyframe.TargetZ = 60.0;

        Assert.Contains(nameof(CameraKeyframe.Time), changedProperties);
        Assert.Contains(nameof(CameraKeyframe.CamX), changedProperties);
        Assert.Contains(nameof(CameraKeyframe.CamY), changedProperties);
        Assert.Contains(nameof(CameraKeyframe.CamZ), changedProperties);
        Assert.Contains(nameof(CameraKeyframe.TargetX), changedProperties);
        Assert.Contains(nameof(CameraKeyframe.TargetY), changedProperties);
        Assert.Contains(nameof(CameraKeyframe.TargetZ), changedProperties);
    }

    [Fact]
    public void EasingPointsData_Getter_ReturnsCorrectData()
    {
        StaTestHelper.RunInSTA(() =>
        {
            var keyframe = new CameraKeyframe();
            var pointsData = keyframe.EasingPointsData;
            Assert.NotNull(pointsData);
            Assert.Equal(2, pointsData.Count);
        });
    }

    [Fact]
    public void EasingPointsData_Setter_UpdatesBezierAnimation()
    {
        StaTestHelper.RunInSTA(() =>
        {
            var keyframe = new CameraKeyframe();
            var newPoints = new List<BezierPointData>
            {
                new() { PX = 0, PY = 0, C1X = 0, C1Y = 0, C2X = 0.5f, C2Y = 0.5f },
                new() { PX = 1, PY = 1, C1X = 0.5f, C1Y = 0.5f, C2X = 1, C2Y = 1 }
            };

            keyframe.EasingPointsData = newPoints;

            var result = keyframe.EasingPointsData;
            Assert.Equal(2, result.Count);
            Assert.Equal(0.5f, result[0].C2X);
            Assert.Equal(0.5f, result[1].C1X);
        });
    }

    [Fact]
    public void EasingIsQuadratic_DelegatesToICameraEasing()
    {
        var easing = new LinearEasing();
        var keyframe = new CameraKeyframe { Easing = easing };

        keyframe.EasingIsQuadratic = true;
        Assert.True(easing.IsQuadratic);
        Assert.True(keyframe.EasingIsQuadratic);

        keyframe.EasingIsQuadratic = false;
        Assert.False(easing.IsQuadratic);
        Assert.False(keyframe.EasingIsQuadratic);
    }
}
