using ObjLoader.Plugin;
using ObjLoader.Plugin.CameraAnimation;
using ObjLoader.ViewModels.Camera;
using System.Collections.ObjectModel;

namespace ObjLoader.UnitTests.ViewModels.Camera
{
    public class CameraKeyframeManagerTests
    {
        [Fact]
        public void AddKeyframe_InsertsAtCorrectPosition()
        {
            RunInSTA(() => {
                var parameter = new ObjLoaderParameter();
                var keyframes = new ObservableCollection<CameraKeyframe>
                {
                    new CameraKeyframe { Time = 1.0, CamX = 10 },
                    new CameraKeyframe { Time = 3.0, CamX = 30 }
                };

                CameraKeyframe? selectedKeyframe = null;
                bool animationUpdated = false;

                var manager = new CameraKeyframeManager(
                    parameter,
                    keyframes,
                    () => 2.0,
                    () => (20.0, 0, 0, 0, 0, 0),
                    kf => selectedKeyframe = kf,
                    () => animationUpdated = true,
                    () => { },
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
            });
        }

        [Fact]
        public void RemoveKeyframe_RemovesAndInvokesActions()
        {
            RunInSTA(() => {
                var parameter = new ObjLoaderParameter();
                var keyframeToRemove = new CameraKeyframe { Time = 1.0 };
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

        private static void RunInSTA(System.Action action)
        {
            System.Exception? exception = null;
            var thread = new System.Threading.Thread(() =>
            {
                try { action(); }
                catch (System.Exception ex) { exception = ex; }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (exception != null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
