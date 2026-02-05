using ObjLoader.Services;
using ObjLoader.Services.UI;
using ObjLoader.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ObjLoader.Views
{
    public partial class CameraWindow : Window
    {
        private static readonly IWindowThemeService _themeService = new WindowThemeService();

        private Point _labelDragStart;
        private bool _isLabelDragging;
        private string _draggedLabel = "";

        public static readonly DependencyProperty ThemeBrushProperty = DependencyProperty.Register(
            nameof(ThemeBrush), typeof(Brush), typeof(CameraWindow), new PropertyMetadata(null, OnThemeBrushChanged));

        public Brush ThemeBrush
        {
            get => (Brush)GetValue(ThemeBrushProperty);
            set => SetValue(ThemeBrushProperty, value);
        }

        private static void OnThemeBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CameraWindow window && window.DataContext is CameraWindowViewModel vm && e.NewValue is SolidColorBrush brush)
            {
                vm.UpdateThemeColor(brush.Color);
            }
        }

        public CameraWindow()
        {
            InitializeComponent();
            _themeService.Bind(this);
            Owner = Application.Current.MainWindow;
            DataContextChanged += OnDataContextChanged;
            Closed += OnClosed;

            this.SetResourceReference(ThemeBrushProperty, SystemColors.ControlBrushKey);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
            {
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            }
            if (e.NewValue is INotifyPropertyChanged newVm)
            {
                newVm.PropertyChanged += OnViewModelPropertyChanged;
            }
            if (e.NewValue is CameraWindowViewModel vm)
            {
                if (ThemeBrush is SolidColorBrush brush)
                {
                    vm.UpdateThemeColor(brush.Color);
                }
                vm.RegisterMenuInputBindings(this);
            }
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            if (DataContext is CameraWindowViewModel vm)
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                vm.Dispose();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != null && (e.PropertyName.EndsWith("Min") || e.PropertyName.EndsWith("Max")))
            {
                if (Mouse.Captured is Thumb)
                {
                    Mouse.Capture(null);
                }
            }
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