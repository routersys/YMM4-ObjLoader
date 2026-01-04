using System.Collections.ObjectModel;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Plugin
{
    public class EasingData : Bindable
    {
        private string _name = "Custom";
        private bool _isCustom = true;
        private EasingType _presetType;
        private ObservableCollection<EasingPoint> _points = new ObservableCollection<EasingPoint>();

        public string Name { get => _name; set => Set(ref _name, value); }
        public bool IsCustom { get => _isCustom; set => Set(ref _isCustom, value); }
        public EasingType PresetType { get => _presetType; set => Set(ref _presetType, value); }
        public ObservableCollection<EasingPoint> Points { get => _points; set => Set(ref _points, value); }

        public EasingData()
        {
            Points = new ObservableCollection<EasingPoint>
            {
                new EasingPoint(0, 0) { HandleOutX = 0.1, HandleOutY = 0 },
                new EasingPoint(1, 1) { HandleInX = -0.1, HandleInY = 0 }
            };
        }

        public double Evaluate(double t)
        {
            if (Points == null || Points.Count < 2) return t;

            var sortedPoints = Points.OrderBy(p => p.X).ToList();
            if (t <= sortedPoints.First().X) return sortedPoints.First().Y;
            if (t >= sortedPoints.Last().X) return sortedPoints.Last().Y;

            for (int i = 0; i < sortedPoints.Count - 1; i++)
            {
                var p1 = sortedPoints[i];
                var p2 = sortedPoints[i + 1];
                if (t >= p1.X && t <= p2.X)
                {
                    double x0 = p1.X;
                    double y0 = p1.Y;
                    double x1 = p1.X + p1.HandleOutX;
                    double y1 = p1.Y + p1.HandleOutY;
                    double x2 = p2.X + p2.HandleInX;
                    double y2 = p2.Y + p2.HandleInY;
                    double x3 = p2.X;
                    double y3 = p2.Y;

                    double tBez = SolveBezierX(t, x0, x1, x2, x3);
                    return GetBezierValue(tBez, y0, y1, y2, y3);
                }
            }
            return t;
        }

        private double SolveBezierX(double x, double x0, double x1, double x2, double x3)
        {
            double t = (x - x0) / (x3 - x0);
            if (t < 0) t = 0;
            if (t > 1) t = 1;

            double t0 = 0;
            double t1 = 1;

            if (Math.Abs(x - x0) < 1e-6) return 0;
            if (Math.Abs(x - x3) < 1e-6) return 1;

            for (int i = 0; i < 8; i++)
            {
                double xt = GetBezierValue(t, x0, x1, x2, x3);
                if (Math.Abs(xt - x) < 1e-6) return t;

                double dxt = GetBezierDerivative(t, x0, x1, x2, x3);

                if (Math.Abs(dxt) < 1e-6) break;

                double nextT = t - (xt - x) / dxt;

                if (nextT < 0 || nextT > 1) break;

                t = nextT;
            }

            t0 = 0;
            t1 = 1;
            for (int i = 0; i < 20; i++)
            {
                t = (t0 + t1) / 2;
                double xt = GetBezierValue(t, x0, x1, x2, x3);
                if (Math.Abs(xt - x) < 1e-6) return t;
                if (xt < x) t0 = t;
                else t1 = t;
            }

            return t;
        }

        private double GetBezierValue(double t, double p0, double p1, double p2, double p3)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;
            double uuu = uu * u;
            double ttt = tt * t;

            return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
        }

        private double GetBezierDerivative(double t, double p0, double p1, double p2, double p3)
        {
            double u = 1 - t;
            return 3 * u * u * (p1 - p0) + 6 * u * t * (p2 - p1) + 3 * t * t * (p3 - p2);
        }

        public EasingData Clone()
        {
            var newData = new EasingData
            {
                Name = Name,
                IsCustom = IsCustom,
                PresetType = PresetType,
                Points = new ObservableCollection<EasingPoint>()
            };
            foreach (var p in Points) newData.Points.Add(p.Clone());
            return newData;
        }

        public override string ToString() => Name;
    }
}