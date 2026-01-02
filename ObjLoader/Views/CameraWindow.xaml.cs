using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ObjLoader.ViewModels;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Controls;

namespace ObjLoader.Localization
{
    public class StringVisibilityConverter : IValueConverter
    {
        public static StringVisibilityConverter Instance = new StringVisibilityConverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}

namespace ObjLoader.Converters
{
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }
}

namespace ObjLoader.Views
{
    public partial class CameraWindow : Window
    {
        private Point _labelDragStart;
        private bool _isLabelDragging;
        private string _draggedLabel = "";

        public CameraWindow()
        {
            InitializeComponent();
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is CameraWindowViewModel vm)
            {
                vm.Zoom(e.Delta);
            }
        }

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is CameraWindowViewModel vm)
            {
                var pos = e.GetPosition(MainViewport);

                if (e.ChangedButton == MouseButton.Left)
                {
                    var hitResult = VisualTreeHelper.HitTest(MainViewport, pos);
                    if (hitResult is RayMeshGeometry3DHitTestResult meshHit)
                    {
                        vm.CheckGizmoHit(meshHit.ModelHit);
                    }
                    else
                    {
                        vm.CheckGizmoHit(null);
                    }
                    vm.StartGizmoDrag(pos);
                }
                else if (e.ChangedButton == MouseButton.Right)
                {
                    vm.StartRotate(pos);
                }
            }
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is CameraWindowViewModel vm)
            {
                vm.EndDrag();
            }
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (DataContext is CameraWindowViewModel vm)
            {
                if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed)
                {
                    vm.Move(e.GetPosition(MainViewport));
                }
            }
        }

        private void GizmoViewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is CameraWindowViewModel vm)
            {
                var pos = e.GetPosition(GizmoViewport);
                var hitResult = VisualTreeHelper.HitTest(GizmoViewport, pos);
                if (hitResult is RayMeshGeometry3DHitTestResult meshHit)
                {
                    vm.HandleViewCubeClick(meshHit.ModelHit);
                }
            }
        }

        private void GizmoViewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (DataContext is CameraWindowViewModel vm)
            {
                var pos = e.GetPosition(GizmoViewport);
                var hitResult = VisualTreeHelper.HitTest(GizmoViewport, pos);
                if (hitResult is RayMeshGeometry3DHitTestResult meshHit)
                {
                    vm.HandleGizmoMove(meshHit.ModelHit);
                }
                else
                {
                    vm.HandleGizmoMove(null);
                }
            }
        }

        private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is CameraWindowViewModel vm)
            {
                vm.ResizeViewport((int)e.NewSize.Width, (int)e.NewSize.Height);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is CameraWindowViewModel vm)
            {
                if (e.Key == Key.F) vm.PerformFocus();
                if (vm.IsPilotView)
                {
                    double fwd = 0, right = 0, up = 0;
                    if (e.Key == Key.W) fwd = 1;
                    if (e.Key == Key.S) fwd = -1;
                    if (e.Key == Key.A) right = -1;
                    if (e.Key == Key.D) right = 1;
                    if (e.Key == Key.Q) up = -1;
                    if (e.Key == Key.E) up = 1;
                    if (fwd != 0 || right != 0 || up != 0) vm.MovePilot(fwd, right, up);
                }
            }
        }

        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb)
            {
                _isLabelDragging = true;
                _labelDragStart = e.GetPosition(this);
                _draggedLabel = tb.Tag.ToString() ?? "";
                tb.CaptureMouse();
            }
        }

        private void Label_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isLabelDragging && DataContext is CameraWindowViewModel vm)
            {
                var current = e.GetPosition(this);
                var delta = current.X - _labelDragStart.X;
                _labelDragStart = current;
                vm.ScrubValue(_draggedLabel, delta);
            }
        }

        private void Label_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isLabelDragging && sender is TextBlock tb)
            {
                _isLabelDragging = false;
                tb.ReleaseMouseCapture();
            }
        }
    }
}