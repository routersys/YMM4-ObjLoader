using ObjLoader.Services.Mmd.Animation;
using ObjLoader.Services.Mmd.Parsers;
using System.Numerics;

namespace ObjLoader.UnitTests.Services.Mmd.Animation
{
    public class DefaultMotionApplierTests
    {
        [Fact]
        public void ConvertCameraFrames_WithCameraFrames_ReturnsCameraKeyframes()
        {
            RunInSTA(() => {
                var applier = new DefaultMotionApplier();
                var vmdData = new VmdData
                {
                    CameraFrames = new List<VmdCameraFrame>
                    {
                        new VmdCameraFrame
                        {
                            FrameNumber = 30,
                            Distance = 100,
                            Position = new Vector3(10, 20, 30),
                            Rotation = new Vector3(0, 0, 0)
                        }
                    }
                };

                var keyframes = applier.ConvertCameraFrames(vmdData, Vector3.Zero, 1.0f);

                Assert.Single(keyframes);
                Assert.Equal(1.0, keyframes[0].Time);
                Assert.NotNull(keyframes[0].Easing);
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
