using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ObjLoader.Plugin.CameraAnimation;

namespace ObjLoader.Converters
{
    public class EasingToGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EasingData data && data.Points != null && data.Points.Count >= 2)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    var sorted = data.Points.OrderBy(p => p.X).ToList();
                    var startPt = sorted[0];
                    ctx.BeginFigure(new Point(startPt.X, 1.0 - startPt.Y), false, false);

                    for (int i = 0; i < sorted.Count - 1; i++)
                    {
                        var p1 = sorted[i];
                        var p2 = sorted[i + 1];

                        var cp1 = new Point(p1.X + p1.HandleOutX, 1.0 - (p1.Y + p1.HandleOutY));
                        var cp2 = new Point(p2.X + p2.HandleInX, 1.0 - (p2.Y + p2.HandleInY));
                        var end = new Point(p2.X, 1.0 - p2.Y);

                        ctx.BezierTo(cp1, cp2, end, true, true);
                    }
                }
                geometry.Freeze();
                return geometry;
            }
            return Geometry.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}