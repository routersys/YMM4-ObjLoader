using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using YukkuriMovieMaker.Commons;
using EasingMode = System.Windows.Media.Animation.EasingMode;

namespace ObjLoader.Views.Controls
{
    public partial class SettingButton : UserControl, IPropertyEditorControl
    {
        event EventHandler? IPropertyEditorControl.BeginEdit { add { } remove { } }
        event EventHandler? IPropertyEditorControl.EndEdit { add { } remove { } }

        public SettingButton()
        {
            InitializeComponent();
        }

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button btn && btn.Content is Grid grid)
            {
                var textBlock = grid.Children.OfType<TextBlock>().FirstOrDefault();
                if (textBlock != null)
                {
                    textBlock.Visibility = Visibility.Visible;
                    textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                    var targetWidth = btn.ActualHeight + textBlock.DesiredSize.Width;

                    var parent = VisualTreeHelper.GetParent(this) as FrameworkElement;
                    while (parent != null && !(parent is Window) && !(parent is Page) && parent.Parent != null)
                    {
                        parent = VisualTreeHelper.GetParent(parent) as FrameworkElement;
                    }

                    if (parent != null)
                    {
                        var p1 = this.TranslatePoint(new Point(0, 0), parent);
                        if (p1.X + targetWidth > parent.ActualWidth)
                        {
                            return;
                        }
                    }

                    var sb = new Storyboard();

                    var widthAnim = new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(200))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                        FillBehavior = FillBehavior.HoldEnd
                    };
                    Storyboard.SetTarget(widthAnim, btn);
                    Storyboard.SetTargetProperty(widthAnim, new PropertyPath("Width"));

                    var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                    Storyboard.SetTarget(opacityAnim, textBlock);
                    Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));

                    sb.Children.Add(widthAnim);
                    sb.Children.Add(opacityAnim);
                    sb.Begin();
                }
            }
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button btn && btn.Content is Grid grid)
            {
                var textBlock = grid.Children.OfType<TextBlock>().FirstOrDefault();
                if (textBlock != null)
                {
                    var sb = new Storyboard();

                    var widthAnim = new DoubleAnimation(btn.ActualHeight, TimeSpan.FromMilliseconds(200))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    Storyboard.SetTarget(widthAnim, btn);
                    Storyboard.SetTargetProperty(widthAnim, new PropertyPath("Width"));

                    var opacityAnim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
                    Storyboard.SetTarget(opacityAnim, textBlock);
                    Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));

                    sb.Completed += (s, args) =>
                    {
                        textBlock.Visibility = Visibility.Collapsed;
                        btn.BeginAnimation(Button.WidthProperty, null);
                    };

                    sb.Children.Add(widthAnim);
                    sb.Children.Add(opacityAnim);
                    sb.Begin();
                }
            }
        }
    }
}