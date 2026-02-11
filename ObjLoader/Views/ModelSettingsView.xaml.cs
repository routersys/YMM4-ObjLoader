using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ObjLoader.Views
{
    public partial class ModelSettingsView : UserControl
    {
        public ModelSettingsView()
        {
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                }
            }
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };

            if (sender is ListBox listBox)
            {
                var scrollViewer = GetScrollViewer(listBox);
                if (scrollViewer != null)
                {
                    if ((e.Delta > 0 && scrollViewer.VerticalOffset == 0) ||
                        (e.Delta < 0 && scrollViewer.VerticalOffset == scrollViewer.ScrollableHeight))
                    {
                        e.Handled = true;
                        var parent = ((FrameworkElement)sender).Parent as UIElement;
                        parent?.RaiseEvent(eventArg);
                    }
                }
            }
            else if (sender is DataGrid dataGrid)
            {
                var scrollViewer = GetScrollViewer(dataGrid);
                if (scrollViewer != null)
                {
                    if ((e.Delta > 0 && scrollViewer.VerticalOffset == 0) ||
                        (e.Delta < 0 && scrollViewer.VerticalOffset == scrollViewer.ScrollableHeight))
                    {
                        e.Handled = true;
                        var parent = ((FrameworkElement)sender).Parent as UIElement;
                        parent?.RaiseEvent(eventArg);
                    }
                }
            }
            else
            {
                e.Handled = true;
                var parent = ((FrameworkElement)sender).Parent as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }

        private ScrollViewer GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer scrollViewer) return scrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null!;
        }
    }
}