using ObjLoader.Services;
using ObjLoader.Services.UI;
using System.Windows;

namespace ObjLoader.Views
{
    public partial class SettingWindow : Window
    {
        private static readonly IWindowThemeService _themeService = new WindowThemeService();

        public SettingWindow()
        {
            InitializeComponent();
            _themeService.Bind(this);
        }
    }
}