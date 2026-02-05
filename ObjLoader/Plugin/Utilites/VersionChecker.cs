using ObjLoader.Localization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace ObjLoader.Plugin.Core
{
    public static class VersionChecker
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static bool _checked = false;

        public static async void CheckVersion()
        {
            if (_checked) return;
            _checked = true;

            try
            {
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (currentVersion == null) return;

                var latestVersionTag = await GetLatestVersionTag();
                if (string.IsNullOrEmpty(latestVersionTag)) return;

                var latestVersion = ParseVersion(latestVersionTag);

                if (latestVersion > currentVersion)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (MessageBox.Show(string.Format(Texts.UpdateNotificationMessage, latestVersion),
                            Texts.UpdateNotificationTitle, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "https://github.com/routersys/YMM4-ObjLoader/releases",
                                UseShellExecute = true
                            });
                        }
                    });
                }
            }
            catch
            {
            }
        }

        private static async Task<string?> GetLatestVersionTag()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/routersys/YMM4-ObjLoader/releases/latest");
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("YMM4-ObjLoader", "1.0"));

                using var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var doc = await JsonDocument.ParseAsync(stream);
                    if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                    {
                        return tag.GetString();
                    }
                }
            }
            catch { }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://manjubox.net/api/ymm4plugins/github/detail/routersys/YMM4-ObjLoader");
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("YMM4-ObjLoader", "1.0"));

                using var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var doc = await JsonDocument.ParseAsync(stream);
                    if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                    {
                        return tag.GetString();
                    }
                }
            }
            catch { }

            return null;
        }

        private static Version ParseVersion(string tag)
        {
            var v = tag.TrimStart('v', 'V');
            if (Version.TryParse(v, out var version))
            {
                return version;
            }
            return new Version(0, 0, 0);
        }
    }
}