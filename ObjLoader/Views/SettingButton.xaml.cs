using ObjLoader.Localization;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Views
{
    public partial class SettingButton : UserControl, IPropertyEditorControl
    {
        private static bool _isChecked;

#pragma warning disable CS0067
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;
#pragma warning restore CS0067

        public SettingButton()
        {
            InitializeComponent();
            CheckVersion();
        }

        private async void CheckVersion()
        {
            if (_isChecked) return;
            _isChecked = true;

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("YMM4-ObjLoader");
                var json = await client.GetStringAsync("https://api.github.com/repos/routersys/YMM4-ObjLoader/releases/latest");
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("tag_name", out var tagProp))
                {
                    var tag = tagProp.GetString();
                    if (!string.IsNullOrEmpty(tag))
                    {
                        var verStr = tag.TrimStart('v');
                        if (Version.TryParse(verStr, out var newVer))
                        {
                            var asm = Assembly.GetExecutingAssembly();
                            var curVer = asm.GetName().Version;
                            if (curVer != null && newVer > curVer)
                            {
                                var msg = string.Format(Texts.UpdateAvailableMessage, newVer);
                                if (MessageBox.Show(msg, Texts.UpdateAvailableTitle, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                                {
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = "https://github.com/routersys/YMM4-ObjLoader/releases/latest",
                                        UseShellExecute = true
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}