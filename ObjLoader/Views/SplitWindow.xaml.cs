using ObjLoader.Services;
using ObjLoader.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ObjLoader.Views
{
    public partial class SplitWindow : Window
    {
        private static readonly IWindowThemeService _themeService = new WindowThemeService();

        public SplitWindow()
        {
            InitializeComponent();
            _themeService.Bind(this);
            Loaded += Window_Loaded;
            Unloaded += Window_Unloaded;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            UpdateTheme();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTheme();
            var descriptor = DependencyPropertyDescriptor.FromProperty(BackgroundProperty, typeof(Window));
            if (descriptor != null)
            {
                descriptor.AddValueChanged(this, OnBackgroundChanged);
            }
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            var descriptor = DependencyPropertyDescriptor.FromProperty(BackgroundProperty, typeof(Window));
            if (descriptor != null)
            {
                descriptor.RemoveValueChanged(this, OnBackgroundChanged);
            }
        }

        private void OnBackgroundChanged(object? sender, EventArgs e)
        {
            UpdateTheme();
        }

        private void UpdateTheme()
        {
            if (DataContext is SplitWindowViewModel vm)
            {
                var backgroundBrush = Background as SolidColorBrush;
                if (backgroundBrush != null)
                {
                    vm.UpdateThemeColor(backgroundBrush.Color);
                }
                else
                {
                    var sysBrush = SystemColors.WindowBrush;
                    if (sysBrush != null)
                    {
                        vm.UpdateThemeColor(sysBrush.Color);
                    }
                    else
                    {
                        vm.UpdateThemeColor(Colors.White);
                    }
                }
            }
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is SplitWindowViewModel vm)
            {
                vm.Zoom(e.Delta);
            }
        }

        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is SplitWindowViewModel vm)
            {
                vm.StartInteraction(e.GetPosition((IInputElement)sender), e.ChangedButton);
            }
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is SplitWindowViewModel vm)
            {
                vm.EndInteraction();
            }
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (DataContext is SplitWindowViewModel vm)
            {
                vm.MoveInteraction(e.GetPosition((IInputElement)sender), e.LeftButton == MouseButtonState.Pressed, e.MiddleButton == MouseButtonState.Pressed, e.RightButton == MouseButtonState.Pressed);
            }
        }

        private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is SplitWindowViewModel vm)
            {
                vm.Resize((int)e.NewSize.Width, (int)e.NewSize.Height);
            }
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            if (DataContext is System.IDisposable d)
            {
                d.Dispose();
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is SplitWindowViewModel vm && sender is ListView lv)
            {
                vm.SelectedPartItems = lv.SelectedItems.Cast<SplitWindowViewModel.PartItem>().ToList();
            }
        }
    }
}