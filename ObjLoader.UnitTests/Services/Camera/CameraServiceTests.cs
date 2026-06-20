using ObjLoader.Plugin.CameraAnimation;
using ObjLoader.Services.Camera;

namespace ObjLoader.UnitTests.Services.Camera
{
    public class CameraServiceTests
    {
        private readonly CameraService _cameraService;

        public CameraServiceTests()
        {
            _cameraService = new CameraService();
        }

        [Fact]
        public void CalculateCameraState_EmptyKeyframes_ReturnsZeros()
        {
            RunInSTA(() => {
                var keyframes = new List<CameraKeyframe>();
                var result = _cameraService.CalculateCameraState(keyframes, 1.0);

                Assert.Equal((0, 0, 0, 0, 0, 0), result);
            });
        }

        [Fact]
        public void CalculateCameraState_SingleKeyframe_ReturnsKeyframeState()
        {
            RunInSTA(() => {
                var keyframes = new List<CameraKeyframe>
                {
                    new CameraKeyframe { Time = 1.0, CamX = 10, TargetX = 20 }
                };

                var result = _cameraService.CalculateCameraState(keyframes, 2.0);

                Assert.Equal(10, result.cx);
                Assert.Equal(20, result.tx);
            });
        }

        [Fact]
        public void CalculateCameraState_BetweenKeyframes_Interpolates()
        {
            RunInSTA(() => {
                var keyframes = new List<CameraKeyframe>
                {
                    new CameraKeyframe { Time = 1.0, CamX = 0, TargetY = 0 },
                    new CameraKeyframe { Time = 3.0, CamX = 10, TargetY = 20 }
                };

                var result = _cameraService.CalculateCameraState(keyframes, 2.0);

                Assert.Equal(5.0, result.cx, 3);
                Assert.Equal(10.0, result.ty, 3);
            });
        }

        [Fact]
        public void CalculateCameraState_BeforeFirstKeyframe_ReturnsFirstKeyframe()
        {
            RunInSTA(() => {
                var keyframes = new List<CameraKeyframe>
                {
                    new CameraKeyframe { Time = 2.0, CamX = 10 },
                    new CameraKeyframe { Time = 4.0, CamX = 20 }
                };

                var result = _cameraService.CalculateCameraState(keyframes, 1.0);

                Assert.Equal(10.0, result.cx);
            });
        }

        [Fact]
        public void CalculateCameraState_AfterLastKeyframe_ReturnsLastKeyframe()
        {
            RunInSTA(() => {
                var keyframes = new List<CameraKeyframe>
                {
                    new CameraKeyframe { Time = 2.0, CamX = 10 },
                    new CameraKeyframe { Time = 4.0, CamX = 20 }
                };

                var result = _cameraService.CalculateCameraState(keyframes, 5.0);

                Assert.Equal(20.0, result.cx);
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
