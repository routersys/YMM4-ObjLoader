using ObjLoader.Services;
using ObjLoader.Services.UI;
using ObjLoader.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ObjLoader.Views
{
    public partial class CenterPointWindow : Window
    {
        private static readonly IWindowThemeService _themeService = new WindowThemeService();

        private Point _lastMousePos;
        private bool _isRotating;
        private bool _isPanning;

        public CenterPointWindow()
        {
            InitializeComponent();
            _themeService.Bind(this);
            MainViewport.MouseMove += MainViewport_MouseMove;
            MainViewport.MouseLeftButtonDown += MainViewport_MouseLeftButtonDown;
            MainViewport.MouseRightButtonDown += MainViewport_MouseRightButtonDown;
            MainViewport.MouseRightButtonUp += MainViewport_MouseRightButtonUp;
            MainViewport.MouseWheel += MainViewport_MouseWheel;
            MainViewport.MouseDown += MainViewport_MouseDown;
            MainViewport.MouseUp += MainViewport_MouseUp;
            CloseButton.Click += (s, e) => this.Close();
        }

        private void MainViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is CenterPointWindowViewModel vm)
            {
                vm.ToggleLockCommand.Execute(null);
            }
        }

        private void MainViewport_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePos = e.GetPosition(MainViewport);
            _isRotating = true;
            MainViewport.CaptureMouse();
        }

        private void MainViewport_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isRotating = false;
            MainViewport.ReleaseMouseCapture();
        }

        private void MainViewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                _lastMousePos = e.GetPosition(MainViewport);
                _isPanning = true;
                MainViewport.CaptureMouse();
            }
        }

        private void MainViewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Released)
            {
                _isPanning = false;
                MainViewport.ReleaseMouseCapture();
            }
        }

        private void MainViewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is CenterPointWindowViewModel vm)
            {
                vm.ZoomCamera(e.Delta);
            }
        }

        private void MainViewport_MouseMove(object sender, MouseEventArgs e)
        {
            var currentPos = e.GetPosition(MainViewport);
            var delta = currentPos - _lastMousePos;

            if (DataContext is CenterPointWindowViewModel vm)
            {
                if (_isRotating)
                {
                    vm.RotateCamera(delta.X, delta.Y);
                    _lastMousePos = currentPos;
                }
                else if (_isPanning)
                {
                    vm.PanCamera(delta.X, delta.Y);
                    _lastMousePos = currentPos;
                }
                else if (!vm.IsLocked)
                {
                    var hitParams = new PointHitTestParameters(currentPos);

                    VisualTreeHelper.HitTest(MainViewport, null, ResultCallback, hitParams);

                    HitTestResultBehavior ResultCallback(HitTestResult result)
                    {
                        if (result is RayMeshGeometry3DHitTestResult meshResult)
                        {
                            vm.UpdateHoverState(meshResult.PointHit,
                                meshResult.VertexIndex1, meshResult.VertexIndex2, meshResult.VertexIndex3);
                            return HitTestResultBehavior.Stop;
                        }
                        return HitTestResultBehavior.Continue;
                    }
                }
            }
        }
    }
}