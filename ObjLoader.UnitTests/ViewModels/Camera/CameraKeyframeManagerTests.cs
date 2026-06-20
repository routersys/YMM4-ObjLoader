using ObjLoader.Plugin;
using ObjLoader.Plugin.CameraAnimation;
using ObjLoader.UnitTests.Infrastructure;
using ObjLoader.ViewModels.Camera;
using System.Collections.ObjectModel;

namespace ObjLoader.UnitTests.ViewModels.Camera;

public class CameraKeyframeManagerTests
{
    [Fact]
    public void AddKeyframe_InsertsAtCorrectPosition()
    {
        StaTestHelper.RunInSTA(() =>
        {
            var parameter = new ObjLoaderParameter();
            var keyframes = new ObservableCollection<CameraKeyframe>
            {
                new() { Easing = new LinearEasing(), Time = 1.0, CamX = 10 },
                new() { Easing = new LinearEasing(), Time = 3.0, CamX = 30 }
            };

            CameraKeyframe? selectedKeyframe = null;
            bool animationUpdated = false;
            bool synced = false;

            var manager = new CameraKeyframeManager(
                parameter,
                keyframes,
                () => 2.0,
                () => (20.0, 0, 0, 0, 0, 0),
                kf => selectedKeyframe = kf,
                () => animationUpdated = true,
                () => synced = true,
                () => { }
            );

            manager.AddKeyframe(null);

            Assert.Equal(3, keyframes.Count);
            Assert.Equal(2.0, keyframes[1].Time);
            Assert.Equal(20.0, keyframes[1].CamX);
            Assert.NotNull(selectedKeyframe);
            Assert.Equal(2.0, selectedKeyframe.Time);
            Assert.Equal(3, parameter.Keyframes.Count);
            Assert.True(animationUpdated);
            Assert.True(synced);
        });
    }

    [Fact]
    public void AddKeyframe_ReplacesExistingKeyframeAtSameTime()
    {
        StaTestHelper.RunInSTA(() =>
        {
            var parameter = new ObjLoaderParameter();
            var keyframes = new ObservableCollection<CameraKeyframe>
            {
                new() { Easing = new LinearEasing(), Time = 1.0, CamX = 10 }
            };

            var manager = new CameraKeyframeManager(
                parameter,
                keyframes,
                () => 1.0,
                () => (99.0, 0, 0, 0, 0, 0),
                _ => { },
                () => { },
                () => { },
                () => { }
            );

            manager.AddKeyframe(null);

            Assert.Single(keyframes);
            Assert.Equal(99.0, keyframes[0].CamX);
        });
    }

    [Fact]
    public void RemoveKeyframe_RemovesAndInvokesCallbacks()
    {
        StaTestHelper.RunInSTA(() =>
        {
            var parameter = new ObjLoaderParameter();
            var keyframeToRemove = new CameraKeyframe { Easing = new LinearEasing(), Time = 1.0 };
            var keyframes = new ObservableCollection<CameraKeyframe> { keyframeToRemove };

            CameraKeyframe? selectedKeyframe = keyframeToRemove;
            bool animationUpdated = false;
            bool synced = false;
            bool resetCalled = false;

            var manager = new CameraKeyframeManager(
                parameter,
                keyframes,
                () => 0.0,
                () => (0, 0, 0, 0, 0, 0),
                kf => selectedKeyframe = kf,
                () => animationUpdated = true,
                () => synced = true,
                () => resetCalled = true
            );

            manager.RemoveKeyframe(keyframeToRemove);

            Assert.Empty(keyframes);
            Assert.Null(selectedKeyframe);
            Assert.True(animationUpdated);
            Assert.True(synced);
            Assert.True(resetCalled);
        });
    }

    [Fact]
    public void RemoveKeyframe_NullArgument_DoesNothing()
    {
        StaTestHelper.RunInSTA(() =>
        {
            var parameter = new ObjLoaderParameter();
            var keyframe = new CameraKeyframe { Easing = new LinearEasing(), Time = 1.0 };
            var keyframes = new ObservableCollection<CameraKeyframe> { keyframe };
            bool animationUpdated = false;

            var manager = new CameraKeyframeManager(
                parameter,
                keyframes,
                () => 0.0,
                () => (0, 0, 0, 0, 0, 0),
                _ => { },
                () => animationUpdated = true,
                () => { },
                () => { }
            );

            manager.RemoveKeyframe(null);

            Assert.Single(keyframes);
            Assert.False(animationUpdated);
        });
    }
}
