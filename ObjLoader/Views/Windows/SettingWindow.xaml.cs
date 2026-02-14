using ObjLoader.Services.UI;
using System.Windows;

namespace ObjLoader.Views.Windows
{
    public partial class SettingWindow : Window
    {
        private readonly IWindowThemeService _themeService = new WindowThemeService();

        public SettingWindow()
        {
            InitializeComponent();
            _themeService.Bind(this);
        }
    }
}