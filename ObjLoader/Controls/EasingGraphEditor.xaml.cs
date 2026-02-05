using ObjLoader.Plugin.CameraAnimation;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ObjLoader.Controls
{
    public partial class EasingGraphEditor : UserControl
    {
        public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
            nameof(Points), typeof(ObservableCollection<EasingPoint>), typeof(EasingGraphEditor), new PropertyMetadata(null, OnPointsChanged));

        public ObservableCollection<EasingPoint> Points
        {
            get => (ObservableCollection<EasingPoint>)GetValue(PointsProperty);
            set => SetValue(PointsProperty, value);
        }

        private double _scale = 1.0;
        private double _offsetX = 0;
        private double _offsetY = 0;
        private bool _isPanning;
        private Point _lastMousePos;

        public EasingGraphEditor()
        {
            InitializeComponent();
            SizeChanged += (s, e) => UpdateVisuals();
        }

        private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EasingGraphEditor editor)
            {
                if (e.OldValue is ObservableCollection<EasingPoint> oldColl)
                {
                    oldColl.CollectionChanged -= editor.OnCollectionChanged;
                    foreach (var p in oldColl) p.PropertyChanged -= editor.OnPointPropertyChanged;
                }
                if (e.NewValue is ObservableCollection<EasingPoint> newColl)
                {
                    newColl.CollectionChanged += editor.OnCollectionChanged;
                    foreach (var p in newColl) p.PropertyChanged += editor.OnPointPropertyChanged;
                }
                editor.Draw();
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (EasingPoint p in e.NewItems) p.PropertyChanged += OnPointPropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (EasingPoint p in e.OldItems) p.PropertyChanged -= OnPointPropertyChanged;
            }
            Draw();
        }

        private void OnPointPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateVisuals();
        }

        private Point ModelToView(double mx, double my, double w, double h)
        {
            return new Point(mx * w * _scale + _offsetX, h - (my * h * _scale) + _offsetY);
        }

        private Point ViewToModel(double vx, double vy, double w, double h)
        {
            if (w == 0 || h == 0 || _scale == 0) return new Point(0, 0);

            double mx = (vx - _offsetX) / (w * _scale);
            double my = (h + _offsetY - vy) / (h * _scale);
            return new Point(mx, my);
        }

        private void Draw()
        {
            PointCanvas.Children.Clear();
            if (Points == null) return;

            foreach (var pt in Points)
            {
                var lineOut = new Line { Stroke = SystemColors.ControlTextBrush, StrokeThickness = 1, Opacity = 0.5, Tag = new Tuple<EasingPoint, string>(pt, "LineOut") };
                PointCanvas.Children.Add(lineOut);

                var lineIn = new Line { Stroke = SystemColors.ControlTextBrush, StrokeThickness = 1, Opacity = 0.5, Tag = new Tuple<EasingPoint, string>(pt, "LineIn") };
                PointCanvas.Children.Add(lineIn);

                var handleOut = new Thumb { Style = (Style)Resources["HandleThumb"], Tag = new Tuple<EasingPoint, bool>(pt, false) };
                handleOut.DragDelta += Handle_DragDelta;
                PointCanvas.Children.Add(handleOut);

                var handleIn = new Thumb { Style = (Style)Resources["HandleThumb"], Tag = new Tuple<EasingPoint, bool>(pt, true) };
                handleIn.DragDelta += Handle_DragDelta;
                PointCanvas.Children.Add(handleIn);

                var anchor = new Thumb { Style = (Style)Resources["AnchorThumb"], Tag = pt };
                anchor.DragDelta += Anchor_DragDelta;
                anchor.PreviewMouseRightButtonUp += Anchor_RightClick;
                PointCanvas.Children.Add(anchor);
            }

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            var w = ActualWidth;
            var h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            var p00 = ModelToView(0, 0, w, h);
            var p11 = ModelToView(1, 1, w, h);

            double rectLeft = Math.Min(p00.X, p11.X);
            double rectTop = Math.Min(p00.Y, p11.Y);
            double rectWidth = Math.Abs(p11.X - p00.X);
            double rectHeight = Math.Abs(p00.Y - p11.Y);

            Canvas.SetLeft(ReferenceFrame, rectLeft);
            Canvas.SetTop(ReferenceFrame, rectTop);
            ReferenceFrame.Width = rectWidth;
            ReferenceFrame.Height = rectHeight;

            GridLine.X1 = p00.X; GridLine.Y1 = p00.Y;
            GridLine.X2 = p11.X; GridLine.Y2 = p11.Y;

            var centerPt = ModelToView(0.5, 0.5, w, h);
            CenterLineX.X1 = centerPt.X; CenterLineX.Y1 = rectTop;
            CenterLineX.X2 = centerPt.X; CenterLineX.Y2 = rectTop + rectHeight;
            CenterLineY.X1 = rectLeft; CenterLineY.Y1 = centerPt.Y;
            CenterLineY.X2 = rectLeft + rectWidth; CenterLineY.Y2 = centerPt.Y;

            if (Points == null || Points.Count < 2)
            {
                GraphLine.Data = null;
                return;
            }

            var sorted = Points.OrderBy(pt => pt.X).ToList();

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var startP = ModelToView(sorted[0].X, sorted[0].Y, w, h);
                ctx.BeginFigure(startP, false, false);

                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    var p1 = sorted[i];
                    var p2 = sorted[i + 1];

                    var cp1 = ModelToView(p1.X + p1.HandleOutX, p1.Y + p1.HandleOutY, w, h);
                    var cp2 = ModelToView(p2.X + p2.HandleInX, p2.Y + p2.HandleInY, w, h);
                    var end = ModelToView(p2.X, p2.Y, w, h);

                    ctx.BezierTo(cp1, cp2, end, true, true);
                }
            }
            GraphLine.Data = geometry;

            foreach (UIElement child in PointCanvas.Children)
            {
                if (child is FrameworkElement elem && elem.Tag != null)
                {
                    if (elem.Tag is EasingPoint anchorPt)
                    {
                        var pos = ModelToView(anchorPt.X, anchorPt.Y, w, h);
                        Canvas.SetLeft(elem, pos.X - ((Thumb)elem).Width / 2);
                        Canvas.SetTop(elem, pos.Y - ((Thumb)elem).Height / 2);
                    }
                    else if (elem.Tag is Tuple<EasingPoint, bool> handleInfo)
                    {
                        var handlePt = handleInfo.Item1;
                        bool isIn = handleInfo.Item2;

                        double hx = isIn ? handlePt.HandleInX : handlePt.HandleOutX;
                        double hy = isIn ? handlePt.HandleInY : handlePt.HandleOutY;

                        bool isFirst = handlePt == sorted.First();
                        bool isLast = handlePt == sorted.Last();
                        bool isVisible = (isIn && !isFirst) || (!isIn && !isLast);

                        elem.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                        if (isVisible)
                        {
                            var pos = ModelToView(handlePt.X + hx, handlePt.Y + hy, w, h);
                            Canvas.SetLeft(elem, pos.X - ((Thumb)elem).Width / 2);
                            Canvas.SetTop(elem, pos.Y - ((Thumb)elem).Height / 2);
                        }
                    }
                    else if (elem.Tag is Tuple<EasingPoint, string> lineInfo)
                    {
                        var linePt = lineInfo.Item1;
                        string type = lineInfo.Item2;
                        bool isIn = type == "LineIn";

                        double hx = isIn ? linePt.HandleInX : linePt.HandleOutX;
                        double hy = isIn ? linePt.HandleInY : linePt.HandleOutY;

                        bool isFirst = linePt == sorted.First();
                        bool isLast = linePt == sorted.Last();
                        bool isVisible = (isIn && !isFirst) || (!isIn && !isLast);

                        elem.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                        if (isVisible)
                        {
                            var line = (Line)elem;
                            var start = ModelToView(linePt.X, linePt.Y, w, h);
                            var end = ModelToView(linePt.X + hx, linePt.Y + hy, w, h);
                            line.X1 = start.X;
                            line.Y1 = start.Y;
                            line.X2 = end.X;
                            line.Y2 = end.Y;
                        }
                    }
                }
            }
        }

        private void Anchor_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb && thumb.Tag is EasingPoint point)
            {
                var w = ActualWidth;
                var h = ActualHeight;
                if (w == 0 || h == 0) return;

                double dx = e.HorizontalChange / (w * _scale);
                double dy = -(e.VerticalChange / (h * _scale));

                double nx = point.X + dx;
                double ny = point.Y + dy;

                nx = Math.Max(0, Math.Min(1, nx));
                ny = Math.Max(0, Math.Min(1, ny));

                var sorted = Points.OrderBy(pt => pt.X).ToList();
                if (point == sorted.First()) nx = 0;
                if (point == sorted.Last()) nx = 1;

                point.X = nx;
                point.Y = ny;
            }
        }

        private void Handle_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb && thumb.Tag is Tuple<EasingPoint, bool> info)
            {
                var point = info.Item1;
                bool isIn = info.Item2;
                var w = ActualWidth;
                var h = ActualHeight;
                if (w == 0 || h == 0) return;

                double dx = e.HorizontalChange / (w * _scale);
                double dy = -(e.VerticalChange / (h * _scale));

                if (isIn)
                {
                    point.HandleInX += dx;
                    point.HandleInY += dy;
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
                    {
                        point.HandleOutX = -point.HandleInX;
                        point.HandleOutY = -point.HandleInY;
                    }
                }
                else
                {
                    point.HandleOutX += dx;
                    point.HandleOutY += dy;
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
                    {
                        point.HandleInX = -point.HandleOutX;
                        point.HandleInY = -point.HandleOutY;
                    }
                }
            }
        }

        private void Anchor_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Thumb thumb && thumb.Tag is EasingPoint point && Points != null)
            {
                if (Points.Count > 2)
                {
                    Points.Remove(point);
                }
                e.Handled = true;
            }
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && Points != null && Points.Count < 8)
            {
                var pos = e.GetPosition(this);
                var w = ActualWidth;
                var h = ActualHeight;
                if (w == 0 || h == 0) return;

                var modelPos = ViewToModel(pos.X, pos.Y, w, h);
                double nx = Math.Max(0, Math.Min(1, modelPos.X));
                double ny = Math.Max(0, Math.Min(1, modelPos.Y));

                var np = new EasingPoint(nx, ny) { HandleInX = -0.05, HandleOutX = 0.05 };

                int idx = 0;
                var sorted = Points.OrderBy(pt => pt.X).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (sorted[i].X < nx) idx = Points.IndexOf(sorted[i]) + 1;
                }
                if (idx > Points.Count) idx = Points.Count;

                Points.Insert(idx, np);
            }
        }

        private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(this);
            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;

            var modelPos = ViewToModel(pos.X, pos.Y, ActualWidth, ActualHeight);

            _scale *= zoomFactor;
            if (_scale < 0.1) _scale = 0.1;
            if (_scale > 10.0) _scale = 10.0;

            _offsetX = pos.X - modelPos.X * ActualWidth * _scale;
            _offsetY = pos.Y - ActualHeight + modelPos.Y * ActualHeight * _scale;

            UpdateVisuals();
            e.Handled = true;
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                _lastMousePos = e.GetPosition(this);
                this.CaptureMouse();
            }
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                var currentPos = e.GetPosition(this);
                var diff = currentPos - _lastMousePos;
                _offsetX += diff.X;
                _offsetY += diff.Y;
                _lastMousePos = currentPos;
                UpdateVisuals();
            }
        }

        private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                this.ReleaseMouseCapture();
            }
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            _scale = 1.0;
            _offsetX = 0;
            _offsetY = 0;
            UpdateVisuals();
        }
    }
}