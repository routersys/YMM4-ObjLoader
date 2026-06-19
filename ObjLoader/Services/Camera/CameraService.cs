using ObjLoader.Plugin.CameraAnimation;

namespace ObjLoader.Services.Camera
{
    public class CameraService
    {
        public (double cx, double cy, double cz, double tx, double ty, double tz) CalculateCameraState(
            List<CameraKeyframe> keyframes, double time)
        {
            if (keyframes == null || keyframes.Count == 0) return (0, 0, 0, 0, 0, 0);

            int prevIndex = FindPrevIndex(keyframes, time);
            int nextIndex = prevIndex + 1;

            CameraKeyframe? prev = prevIndex >= 0 ? keyframes[prevIndex] : null;
            CameraKeyframe? next = nextIndex < keyframes.Count ? keyframes[nextIndex] : null;

            if (prev == null && next != null)
                return (next.CamX, next.CamY, next.CamZ, next.TargetX, next.TargetY, next.TargetZ);
            if (prev != null && next == null)
                return (prev.CamX, prev.CamY, prev.CamZ, prev.TargetX, prev.TargetY, prev.TargetZ);
            if (prev != null && next != null)
            {
                double span = next.Time - prev.Time;
                double t = span <= 0 ? 1.0 : (time - prev.Time) / span;
                double easedT = prev.Easing.GetAnimation(t);
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

        private static int FindPrevIndex(List<CameraKeyframe> keyframes, double time)
        {
            int lo = 0, hi = keyframes.Count - 1, result = -1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (keyframes[mid].Time <= time) { result = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return result;
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}