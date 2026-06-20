using ObjLoader.Plugin.CameraAnimation;

namespace ObjLoader.UnitTests.Plugin.CameraAnimation
{
    public class CameraKeyframeTests
    {
        [Fact]
        public void PropertyChanges_TriggerPropertyChangedEvent()
        {
            RunInSTA(() => {
                var keyframe = new CameraKeyframe();
                var changedProperties = new List<string>();
                keyframe.PropertyChanged += (sender, e) =>
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
            });
        }

        [Fact]
        public void EasingPointsData_Getter_ReturnsCorrectData()
        {
            RunInSTA(() => {
                var keyframe = new CameraKeyframe();
                var pointsData = keyframe.EasingPointsData;
                Assert.NotNull(pointsData);
                Assert.Equal(2, pointsData.Count);
            });
        }

        [Fact]
        public void EasingPointsData_Setter_UpdatesBezierAnimation()
        {
            RunInSTA(() => {
                var keyframe = new CameraKeyframe();
                var newPoints = new List<BezierPointData>
                {
                    new BezierPointData { PX = 0, PY = 0, C1X = 0, C1Y = 0, C2X = 0.5f, C2Y = 0.5f },
                    new BezierPointData { PX = 1, PY = 1, C1X = 0.5f, C1Y = 0.5f, C2X = 1, C2Y = 1 }
                };

                keyframe.EasingPointsData = newPoints;

                Assert.Equal(2, keyframe.Easing.Points.Count);
                Assert.Equal(0.5f, keyframe.Easing.Points[0].ControlPoint2.X);
                Assert.Equal(0.5f, keyframe.Easing.Points[1].ControlPoint1.X);
            });
        }

        [Fact]
        public void EasingIsQuadratic_DelegatesToBezierAnimation()
        {
            RunInSTA(() => {
                var keyframe = new CameraKeyframe();
                
                keyframe.EasingIsQuadratic = true;
                Assert.True(keyframe.Easing.IsQuadratic);
                Assert.True(keyframe.EasingIsQuadratic);

                keyframe.EasingIsQuadratic = false;
                Assert.False(keyframe.Easing.IsQuadratic);
                Assert.False(keyframe.EasingIsQuadratic);
            });
        }

        private static void RunInSTA(Action action)
        {
            Exception? exception = null;
            var thread = new System.Threading.Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { exception = ex; }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (exception != null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
