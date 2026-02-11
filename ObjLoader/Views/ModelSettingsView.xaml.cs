using System;
using System.Windows;
using System.Windows.Controls;

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
    }
}