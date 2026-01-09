using ObjLoader.Plugin;

namespace ObjLoader.Services
{
    public class CameraService
    {
        public (double cx, double cy, double cz, double tx, double ty, double tz) CalculateCameraState(List<CameraKeyframe> keyframes, double time)
        {
            if (keyframes == null || keyframes.Count == 0) return (0, 0, 0, 0, 0, 0);

            var sorted = keyframes.OrderBy(k => k.Time).ToList();
            var prev = sorted.LastOrDefault(k => k.Time <= time);
            var next = sorted.FirstOrDefault(k => k.Time > time);

            if (prev == null && next != null) return (next.CamX, next.CamY, next.CamZ, next.TargetX, next.TargetY, next.TargetZ);
            if (prev != null && next == null) return (prev.CamX, prev.CamY, prev.CamZ, prev.TargetX, prev.TargetY, prev.TargetZ);
            if (prev != null && next != null)
            {
                double t = (time - prev.Time) / (next.Time - prev.Time);
                double easedT = prev.Easing.Evaluate(t);
                return (
                    Lerp(prev.CamX, next.CamX, easedT),
                    Lerp(prev.CamY, next.CamY, easedT),
                    Lerp(prev.CamZ, next.CamZ, easedT),
                    Lerp(prev.TargetX, next.TargetX, easedT),
                    Lerp(prev.TargetY, next.TargetY, easedT),
                    Lerp(prev.TargetZ, next.TargetZ, easedT)
                );
            }
            return (0, 0, 0, 0, 0, 0);
        }

        private double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }
    }
}