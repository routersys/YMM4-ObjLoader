using System.Collections.Concurrent;
using System.Windows;

namespace ObjLoader.Utilities
{
    internal static class UserNotification
    {
        private static readonly ConcurrentDictionary<string, DateTime> _lastShown = new();
        private static readonly TimeSpan _cooldown = TimeSpan.FromSeconds(10);

        public static void ShowWarning(string message, string title)
        {
            Show(message, title, MessageBoxImage.Warning);
        }

        public static void ShowError(string message, string title)
        {
            Show(message, title, MessageBoxImage.Error);
        }

        private static void Show(string message, string title, MessageBoxImage icon)
        {
            string key = $"{title}:{message}";
            var now = DateTime.UtcNow;

            if (_lastShown.TryGetValue(key, out var last) && (now - last) < _cooldown)
            {
                return;
            }

            _lastShown[key] = now;

            try
            {
                var app = Application.Current;
                if (app == null || app.Dispatcher == null || app.Dispatcher.HasShutdownStarted)
                {
                    return;
                }

                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        MessageBox.Show(message, title, MessageBoxButton.OK, icon);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"UserNotification: Failed to show message box: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UserNotification: Failed to dispatch message box: {ex.Message}");
            }
        }

        public static void ClearHistory()
        {
            _lastShown.Clear();
        }
    }
}