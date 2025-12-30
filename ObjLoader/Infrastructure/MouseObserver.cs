using System.Windows;
using System.Windows.Input;

namespace ObjLoader.Infrastructure
{
    public static class MouseObserver
    {
        public static readonly DependencyProperty ObserveProperty =
            DependencyProperty.RegisterAttached(
                "Observe",
                typeof(bool),
                typeof(MouseObserver),
                new PropertyMetadata(false, OnObserveChanged));

        public static bool GetObserve(DependencyObject obj) => (bool)obj.GetValue(ObserveProperty);
        public static void SetObserve(DependencyObject obj, bool value) => obj.SetValue(ObserveProperty, value);

        public static readonly DependencyProperty IsHoveredProperty =
            DependencyProperty.RegisterAttached(
                "IsHovered",
                typeof(bool),
                typeof(MouseObserver),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static bool GetIsHovered(DependencyObject obj) => (bool)obj.GetValue(IsHoveredProperty);
        public static void SetIsHovered(DependencyObject obj, bool value) => obj.SetValue(IsHoveredProperty, value);

        private static void OnObserveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement fe)
            {
                if ((bool)e.NewValue)
                {
                    fe.MouseEnter += Fe_MouseEnter;
                    fe.MouseLeave += Fe_MouseLeave;
                }
                else
                {
                    fe.MouseEnter -= Fe_MouseEnter;
                    fe.MouseLeave -= Fe_MouseLeave;
                }
            }
        }

        private static void Fe_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is DependencyObject d) SetIsHovered(d, true);
        }

        private static void Fe_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is DependencyObject d) SetIsHovered(d, false);
        }
    }
}