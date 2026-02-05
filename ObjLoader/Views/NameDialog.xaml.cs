using ObjLoader.Services;
using ObjLoader.Services.UI;
using System.Windows;

namespace ObjLoader.Views
{
    public partial class NameDialog : Window
    {
        private static readonly IWindowThemeService _themeService = new WindowThemeService();

        public string ResultName { get; private set; } = "";

        public NameDialog()
        {
            InitializeComponent();
            _themeService.Bind(this);
            NameBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultName = NameBox.Text;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}