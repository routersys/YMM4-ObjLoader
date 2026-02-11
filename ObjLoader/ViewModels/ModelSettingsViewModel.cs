using ObjLoader.Infrastructure;
using ObjLoader.Settings;
using ObjLoader.Utilities;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    internal class ModelSettingsViewModel : Bindable, IDisposable
    {
        private readonly ModelSettings _settings;
        private readonly Action<AuditReport> _auditHandler;
        private bool _disposed;

        public bool IsSandboxEnforced
        {
            get => _settings.IsSandboxEnforced;
            set
            {
                if (_settings.IsSandboxEnforced == value) return;
                _settings.IsSandboxEnforced = value;
                OnPropertyChanged(nameof(IsSandboxEnforced));
                try
                {
                    if (value)
                        FileSystemSandbox.Instance.Enable();
                    else
                        FileSystemSandbox.Instance.Disable();
                }
                catch
                {
                }
            }
        }

        public ObservableCollection<string> AllowedRoots { get; }

        private string _selectedRoot = string.Empty;
        public string SelectedRoot
        {
            get => _selectedRoot;
            set => Set(ref _selectedRoot, value);
        }

        public ICommand AddDirectoryCommand { get; }
        public ICommand RemoveDirectoryCommand { get; }
        public ICommand ClearDirectoriesCommand { get; }

        public bool EnableAutoAudit
        {
            get => _settings.EnableAutoAudit;
            set
            {
                if (_settings.EnableAutoAudit == value) return;
                _settings.EnableAutoAudit = value;
                OnPropertyChanged(nameof(EnableAutoAudit));
                UpdateAuditorState();
            }
        }

        public double AuditIntervalMinutes
        {
            get => _settings.AuditIntervalMinutes;
            set
            {
                double clamped = Math.Max(0.5, Math.Min(1440.0, value));
                if (Math.Abs(_settings.AuditIntervalMinutes - clamped) < 0.001) return;
                _settings.AuditIntervalMinutes = clamped;
                OnPropertyChanged(nameof(AuditIntervalMinutes));
                UpdateAuditorState();
            }
        }

        public double LeakThresholdMinutes
        {
            get => _settings.LeakThresholdMinutes;
            set
            {
                double clamped = Math.Max(1.0, Math.Min(14400.0, value));
                if (Math.Abs(_settings.LeakThresholdMinutes - clamped) < 0.001) return;
                _settings.LeakThresholdMinutes = clamped;
                OnPropertyChanged(nameof(LeakThresholdMinutes));
                try
                {
                    ResourceAuditor.Instance.SetLeakThreshold(TimeSpan.FromMinutes(clamped));
                }
                catch
                {
                }
            }
        }

        public ICommand RunAuditCommand { get; }

        private AuditReport _latestReport = AuditReport.Empty;
        public AuditReport LatestReport
        {
            get => _latestReport;
            private set => Set(ref _latestReport, value);
        }

        public ModelSettingsViewModel(ModelSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            AllowedRoots = new ObservableCollection<string>(_settings.AllowedRoots ?? new System.Collections.Generic.List<string>());

            try
            {
                if (_settings.IsSandboxEnforced)
                    FileSystemSandbox.Instance.Enable();
                else
                    FileSystemSandbox.Instance.Disable();

                foreach (var root in AllowedRoots)
                {
                    if (!string.IsNullOrWhiteSpace(root))
                        FileSystemSandbox.Instance.AddAllowedRoot(root);
                }

                ResourceAuditor.Instance.SetLeakThreshold(TimeSpan.FromMinutes(Math.Max(1.0, _settings.LeakThresholdMinutes)));
                UpdateAuditorState();
            }
            catch
            {
            }

            AddDirectoryCommand = new ActionCommand(
                _ => true,
                _ =>
                {
                    try
                    {
                        var dialog = new Microsoft.Win32.OpenFolderDialog();
                        if (dialog.ShowDialog() == true)
                        {
                            var path = dialog.FolderName;
                            if (!string.IsNullOrWhiteSpace(path) && !AllowedRoots.Contains(path))
                            {
                                AllowedRoots.Add(path);
                                FileSystemSandbox.Instance.AddAllowedRoot(path);
                                SaveRoots();
                            }
                        }
                    }
                    catch
                    {
                    }
                });

            RemoveDirectoryCommand = new ActionCommand(
                _ => !string.IsNullOrEmpty(SelectedRoot),
                _ =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(SelectedRoot))
                        {
                            var root = SelectedRoot;
                            FileSystemSandbox.Instance.RemoveAllowedRoot(root);
                            AllowedRoots.Remove(root);
                            SaveRoots();
                            SelectedRoot = string.Empty;
                        }
                    }
                    catch
                    {
                    }
                });

            ClearDirectoriesCommand = new ActionCommand(
                _ => AllowedRoots.Count > 0,
                _ =>
                {
                    try
                    {
                        FileSystemSandbox.Instance.ClearAllowedRoots();
                        AllowedRoots.Clear();
                        SaveRoots();
                        SelectedRoot = string.Empty;
                    }
                    catch
                    {
                    }
                });

            RunAuditCommand = new ActionCommand(
                _ => true,
                _ =>
                {
                    try
                    {
                        LatestReport = ResourceAuditor.Instance.RunAudit();
                    }
                    catch
                    {
                        LatestReport = AuditReport.Empty;
                    }
                });

            _auditHandler = OnAuditCompleted;
            ResourceAuditor.Instance.AuditCompleted += _auditHandler;
        }

        private void OnAuditCompleted(AuditReport report)
        {
            if (_disposed) return;

            try
            {
                var app = Application.Current;
                if (app != null && app.Dispatcher != null && !app.Dispatcher.HasShutdownStarted)
                {
                    app.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!_disposed)
                        {
                            LatestReport = report;
                        }
                    }));
                }
            }
            catch
            {
            }
        }

        private void SaveRoots()
        {
            try
            {
                _settings.AllowedRoots = AllowedRoots.ToList();
            }
            catch
            {
            }
        }

        private void UpdateAuditorState()
        {
            try
            {
                if (_settings.EnableAutoAudit)
                {
                    ResourceAuditor.Instance.Restart(TimeSpan.FromMinutes(Math.Max(0.5, _settings.AuditIntervalMinutes)));
                }
                else
                {
                    ResourceAuditor.Instance.Stop();
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                ResourceAuditor.Instance.AuditCompleted -= _auditHandler;
            }
            catch
            {
            }
        }
    }
}