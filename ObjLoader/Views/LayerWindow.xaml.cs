using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ObjLoader.ViewModels;

namespace ObjLoader.Views
{
    public partial class LayerWindow : Window
    {
        public LayerWindow()
        {
            InitializeComponent();
        }

        private void LayerName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var tb = (TextBox)sender;
                tb.GetBindingExpression(TextBox.TextProperty).UpdateSource();

                if (tb.DataContext is LayerItemViewModel vm)
                {
                    vm.EndEditCommand.Execute(null);
                }
                e.Handled = true;
            }
        }

        private void LayerName_LostFocus(object sender, RoutedEventArgs e)
        {
            var tb = (TextBox)sender;
            if (tb.DataContext is LayerItemViewModel vm)
            {
                vm.EndEditCommand.Execute(null);
            }
        }

        private void LayerName_Loaded(object sender, RoutedEventArgs e)
        {
            var tb = (TextBox)sender;
            tb.Focus();
            tb.SelectAll();
        }
    }
}