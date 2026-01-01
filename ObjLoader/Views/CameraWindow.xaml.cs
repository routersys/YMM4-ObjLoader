using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ObjLoader.ViewModels;
using System.Globalization;
using System.Windows.Data;

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

namespace ObjLoader.Views
{
    public partial class CameraWindow : Window
    {
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
                        if (meshHit.MeshHit == vm.TargetVisualGeometry)
                        {
                            vm.StartTargetDrag(pos);
                        }
                        else if (meshHit.MeshHit == vm.CameraHandleGeometry)
                        {
                            vm.StartCameraDrag(pos);
                        }
                        else
                        {
                            vm.StartPan(pos);
                        }
                    }
                    else
                    {
                        vm.StartPan(pos);
                    }
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
    }
}