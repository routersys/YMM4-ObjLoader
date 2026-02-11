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

        public int MaxFileSizeMB
        {
            get => _settings.MaxFileSizeMB;
            set
            {
                int clamped = Math.Clamp(value, ModelSettings.MinFileSizeMB, ModelSettings.MaxFileSizeMBLimit);
                if (_settings.MaxFileSizeMB == clamped) return;
                _settings.MaxFileSizeMB = clamped;
                OnPropertyChanged(nameof(MaxFileSizeMB));
            }
        }

        public int MaxGpuMemoryPerModelMB
        {
            get => _settings.MaxGpuMemoryPerModelMB;
            set
            {
                int clamped = Math.Clamp(value, ModelSettings.MinGpuMemoryMB, ModelSettings.MaxGpuMemoryMBLimit);
                if (_settings.MaxGpuMemoryPerModelMB == clamped) return;
                _settings.MaxGpuMemoryPerModelMB = clamped;
                OnPropertyChanged(nameof(MaxGpuMemoryPerModelMB));
            }
        }

        public int MaxTotalGpuMemoryMB
        {
            get => _settings.MaxTotalGpuMemoryMB;
            set
            {
                int clamped = Math.Clamp(value, ModelSettings.MinGpuMemoryMB, ModelSettings.MaxGpuMemoryMBLimit);
                if (_settings.MaxTotalGpuMemoryMB == clamped) return;
                _settings.MaxTotalGpuMemoryMB = clamped;
                OnPropertyChanged(nameof(MaxTotalGpuMemoryMB));
            }
        }

        public int MaxVertices
        {
            get => _settings.MaxVertices;
            set
            {
                int clamped = Math.Clamp(value, ModelSettings.MinVertices, ModelSettings.MaxVerticesLimit);
                if (_settings.MaxVertices == clamped) return;
                _settings.MaxVertices = clamped;
                OnPropertyChanged(nameof(MaxVertices));
            }
        }

        public int MaxIndices
        {
            get => _settings.MaxIndices;
            set
            {
                int clamped = Math.Clamp(value, ModelSettings.MinIndices, ModelSettings.MaxIndicesLimit);
                if (_settings.MaxIndices == clamped) return;
                _settings.MaxIndices = clamped;
                OnPropertyChanged(nameof(MaxIndices));
            }
        }

        public int MaxParts
        {
            get => _settings.MaxParts;
            set
            {
                int clamped = Math.Clamp(value, ModelSettings.MinParts, ModelSettings.MaxPartsLimit);
                if (_settings.MaxParts == clamped) return;
                _settings.MaxParts = clamped;
                OnPropertyChanged(nameof(MaxParts));
            }
        }

        public int MinFileSizeMB => ModelSettings.MinFileSizeMB;
        public int MaxFileSizeMBLimit => ModelSettings.MaxFileSizeMBLimit;
        public int MinGpuMemoryMB => ModelSettings.MinGpuMemoryMB;
        public int MaxGpuMemoryMBLimit => ModelSettings.MaxGpuMemoryMBLimit;
        public int MinVertices => ModelSettings.MinVertices;
        public int MaxVerticesLimit => ModelSettings.MaxVerticesLimit;
        public int MinIndices => ModelSettings.MinIndices;
        public int MaxIndicesLimit => ModelSettings.MaxIndicesLimit;
        public int MinParts => ModelSettings.MinParts;
        public int MaxPartsLimit => ModelSettings.MaxPartsLimit;

        public ICommand RunAuditCommand { get; }
        public ICommand ResetMaxFileSizeMBCommand { get; }
        public ICommand ResetMaxGpuMemoryPerModelMBCommand { get; }
        public ICommand ResetMaxTotalGpuMemoryMBCommand { get; }
        public ICommand ResetMaxVerticesCommand { get; }
        public ICommand ResetMaxIndicesCommand { get; }
        public ICommand ResetMaxPartsCommand { get; }
        public ICommand ResetAuditIntervalMinutesCommand { get; }
        public ICommand ResetLeakThresholdMinutesCommand { get; }
        public ICommand ResetResourceLimitsCommand { get; }
        public ICommand ResetComplexityLimitsCommand { get; }
        public ICommand ResetAuditSettingsCommand { get; }

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

            ResetMaxFileSizeMBCommand = new ActionCommand(_ => true, _ => MaxFileSizeMB = ModelSettings.DefaultMaxFileSizeMB);
            ResetMaxGpuMemoryPerModelMBCommand = new ActionCommand(_ => true, _ => MaxGpuMemoryPerModelMB = ModelSettings.DefaultMaxGpuMemoryPerModelMB);
            ResetMaxTotalGpuMemoryMBCommand = new ActionCommand(_ => true, _ => MaxTotalGpuMemoryMB = ModelSettings.DefaultMaxTotalGpuMemoryMB);
            ResetMaxVerticesCommand = new ActionCommand(_ => true, _ => MaxVertices = ModelSettings.DefaultMaxVertices);
            ResetMaxIndicesCommand = new ActionCommand(_ => true, _ => MaxIndices = ModelSettings.DefaultMaxIndices);
            ResetMaxPartsCommand = new ActionCommand(_ => true, _ => MaxParts = ModelSettings.DefaultMaxParts);
            ResetAuditIntervalMinutesCommand = new ActionCommand(_ => true, _ => AuditIntervalMinutes = 5.0);
            ResetLeakThresholdMinutesCommand = new ActionCommand(_ => true, _ => LeakThresholdMinutes = 30.0);

            ResetResourceLimitsCommand = new ActionCommand(_ => true, _ =>
            {
                MaxFileSizeMB = ModelSettings.DefaultMaxFileSizeMB;
                MaxGpuMemoryPerModelMB = ModelSettings.DefaultMaxGpuMemoryPerModelMB;
                MaxTotalGpuMemoryMB = ModelSettings.DefaultMaxTotalGpuMemoryMB;
            });

            ResetComplexityLimitsCommand = new ActionCommand(_ => true, _ =>
            {
                MaxVertices = ModelSettings.DefaultMaxVertices;
                MaxIndices = ModelSettings.DefaultMaxIndices;
                MaxParts = ModelSettings.DefaultMaxParts;
            });

            ResetAuditSettingsCommand = new ActionCommand(_ => true, _ =>
            {
                AuditIntervalMinutes = 5.0;
                LeakThresholdMinutes = 30.0;
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