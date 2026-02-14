using ObjLoader.Cache;
using ObjLoader.Infrastructure;
using ObjLoader.Localization;
using ObjLoader.Rendering.Core;
using ObjLoader.Settings;
using ObjLoader.Utilities;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels.Settings
{
    internal class ModelSettingsViewModel : Bindable, IDisposable
    {
        private readonly ModelSettings _settings;
        private readonly Action<AuditReport> _auditHandler;
        private bool _disposed;

        private readonly DispatcherTimer _dashboardTimer;
        private readonly CircularBuffer<double> _trackerMemoryHistory = new(60);
        private readonly CircularBuffer<double> _gpuCacheMemoryHistory = new(60);

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

        public double D3DResourceReleaseDelay
        {
            get => _settings.D3DResourceReleaseDelay;
            set
            {
                double clamped = Math.Max(0.0, Math.Min(3600.0, value));
                if (Math.Abs(_settings.D3DResourceReleaseDelay - clamped) < 0.001) return;
                _settings.D3DResourceReleaseDelay = clamped;
                OnPropertyChanged(nameof(D3DResourceReleaseDelay));
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
        public ICommand ResetD3DResourceReleaseDelayCommand { get; }
        public ICommand ResetResourceLimitsCommand { get; }
        public ICommand ResetComplexityLimitsCommand { get; }
        public ICommand ResetAuditSettingsCommand { get; }
        public ICommand ClearGpuCacheCommand { get; }
        public ICommand ForceDisposeLeakedCommand { get; }
        public ICommand ResetAllResourcesCommand { get; }
        public ICommand CopyDashboardCommand { get; }
        public ICommand CopyLeakedResourcesCommand { get; }
        public ICommand CopyOrphanedResourcesCommand { get; }
        public ICommand RefreshGpuCacheCommand { get; }
        public ActionCommand RemoveSelectedCacheCommand { get; }
        public ICommand ClearAllCacheCommand { get; }

        private AuditReport _latestReport = AuditReport.Empty;
        public AuditReport LatestReport
        {
            get => _latestReport;
            private set => Set(ref _latestReport, value);
        }

        private double _estimatedMemoryMB;
        public double EstimatedMemoryMB
        {
            get => _estimatedMemoryMB;
            private set => Set(ref _estimatedMemoryMB, value);
        }

        private int _gpuCacheEntryCount;
        public int GpuCacheEntryCount
        {
            get => _gpuCacheEntryCount;
            private set => Set(ref _gpuCacheEntryCount, value);
        }

        private double _totalGpuCacheMemoryMB;
        public double TotalGpuCacheMemoryMB
        {
            get => _totalGpuCacheMemoryMB;
            private set => Set(ref _totalGpuCacheMemoryMB, value);
        }

        private string _lastUpdatedText = string.Empty;
        public string LastUpdatedText
        {
            get => _lastUpdatedText;
            private set => Set(ref _lastUpdatedText, value);
        }

        private PointCollection _trackerGraphPoints = new();
        public PointCollection TrackerGraphPoints
        {
            get => _trackerGraphPoints;
            private set => Set(ref _trackerGraphPoints, value);
        }

        private PointCollection _gpuCacheGraphPoints = new();
        public PointCollection GpuCacheGraphPoints
        {
            get => _gpuCacheGraphPoints;
            private set => Set(ref _gpuCacheGraphPoints, value);
        }

        private double _graphMaxValue = 1.0;
        public double GraphMaxValue
        {
            get => _graphMaxValue;
            private set => Set(ref _graphMaxValue, value);
        }

        public ObservableCollection<GpuCacheSnapshot> GpuCacheItems { get; } = new();

        private GpuCacheSnapshot? _selectedCacheItem;
        public GpuCacheSnapshot? SelectedCacheItem
        {
            get => _selectedCacheItem;
            set
            {
                if (Set(ref _selectedCacheItem, value))
                    RemoveSelectedCacheCommand?.RaiseCanExecuteChanged();
            }
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
            ResetD3DResourceReleaseDelayCommand = new ActionCommand(_ => true, _ => D3DResourceReleaseDelay = ModelSettings.DefaultD3DResourceReleaseDelay);

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
                D3DResourceReleaseDelay = ModelSettings.DefaultD3DResourceReleaseDelay;
            });

            ClearGpuCacheCommand = new ActionCommand(_ => true, _ =>
            {
                try
                {
                    var result = MessageBox.Show(
                        Texts.ConfirmClearGpuCache,
                        Texts.ConfirmTitle,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (result != MessageBoxResult.Yes) return;

                    lock (ObjLoaderSource.SharedRenderLock)
                    {
                        GpuResourceCache.Instance.Clear();
                    }
                    RefreshDashboard();
                    RefreshGpuCacheList();
                }
                catch
                {
                }
            });

            ForceDisposeLeakedCommand = new ActionCommand(_ => true, _ =>
            {
                try
                {
                    int disposed;
                    lock (ObjLoaderSource.SharedRenderLock)
                    {
                        disposed = ResourceTracker.Instance.ForceDisposeLeaked(
                            TimeSpan.FromMinutes(Math.Max(1.0, _settings.LeakThresholdMinutes)));
                    }
                    LatestReport = ResourceAuditor.Instance.RunAudit();
                    RefreshDashboard();
                    MessageBox.Show(
                        string.Format(Texts.DisposedCount, disposed),
                        Texts.ConfirmTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch
                {
                }
            });

            ResetAllResourcesCommand = new ActionCommand(_ => true, _ =>
            {
                try
                {
                    var result = MessageBox.Show(
                        Texts.ConfirmResetAll,
                        Texts.ConfirmTitle,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (result != MessageBoxResult.Yes) return;

                    lock (ObjLoaderSource.SharedRenderLock)
                    {
                        GpuResourceCache.Instance.Clear();
                        ResourceTracker.Instance.ForceDisposeLeaked(TimeSpan.Zero);
                    }
                    ResourceTracker.Instance.PurgeHistory();
                    LatestReport = ResourceAuditor.Instance.RunAudit();
                    RefreshDashboard();
                    RefreshGpuCacheList();
                }
                catch
                {
                }
            });

            CopyDashboardCommand = new ActionCommand(_ => true, _ =>
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"== {Texts.Dashboard} ==");
                    sb.AppendLine($"{Texts.ActiveResources} {LatestReport.ActiveResources}");
                    sb.AppendLine($"{Texts.OrphanedResources} {LatestReport.OrphanedResources}");
                    sb.AppendLine($"{Texts.TotalAllocations} {LatestReport.TotalAllocations}");
                    sb.AppendLine($"{Texts.TotalDisposals} {LatestReport.TotalDisposals}");
                    sb.AppendLine($"{Texts.EstimatedMemoryMB} {EstimatedMemoryMB:F2}");
                    sb.AppendLine($"{Texts.GpuCacheEntries} {GpuCacheEntryCount}");
                    sb.AppendLine($"{Texts.TotalGpuMemoryMB} {TotalGpuCacheMemoryMB:F2}");

                    Clipboard.SetText(sb.ToString());
                }
                catch
                {
                }
            });

            CopyLeakedResourcesCommand = new ActionCommand(_ => true, _ =>
            {
                try
                {
                    CopyResourceListToClipboard(LatestReport.LeakedResources, Texts.LeakedResourcesList);
                }
                catch
                {
                }
            });

            CopyOrphanedResourcesCommand = new ActionCommand(_ => true, _ =>
            {
                try
                {
                    CopyResourceListToClipboard(LatestReport.OrphanedResourceList, Texts.OrphanedResourcesList);
                }
                catch
                {
                }
            });

            RefreshGpuCacheCommand = new ActionCommand(_ => true, _ => RefreshGpuCacheList());

            RemoveSelectedCacheCommand = new ActionCommand(
                _ => SelectedCacheItem != null,
                _ =>
                {
                    try
                    {
                        if (SelectedCacheItem == null) return;
                        var key = SelectedCacheItem.Key;
                        lock (ObjLoaderSource.SharedRenderLock)
                        {
                            GpuResourceCache.Instance.Remove(key);
                        }
                        RefreshGpuCacheList();
                        RefreshDashboard();
                    }
                    catch
                    {
                    }
                });

            ClearAllCacheCommand = new ActionCommand(_ => true, _ =>
            {
                try
                {
                    lock (ObjLoaderSource.SharedRenderLock)
                    {
                        GpuResourceCache.Instance.Clear();
                    }
                    RefreshGpuCacheList();
                    RefreshDashboard();
                }
                catch
                {
                }
            });

            _auditHandler = OnAuditCompleted;
            ResourceAuditor.Instance.AuditCompleted += _auditHandler;


            _dashboardTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _dashboardTimer.Tick += OnDashboardTimerTick;
            _dashboardTimer.Start();


            RefreshDashboard();
            RefreshGpuCacheList();
        }

        private void OnDashboardTimerTick(object? sender, EventArgs e)
        {
            if (_disposed) return;
            RefreshDashboard();
        }

        private void RefreshDashboard()
        {
            try
            {
                var stats = ResourceTracker.Instance.GetStats();
                double memMB = stats.EstimatedActiveBytes / (1024.0 * 1024.0);
                EstimatedMemoryMB = Math.Round(memMB, 2);

                var cache = GpuResourceCache.Instance;
                GpuCacheEntryCount = cache.Count;
                double gpuMB = cache.TotalEstimatedBytes / (1024.0 * 1024.0);
                TotalGpuCacheMemoryMB = Math.Round(gpuMB, 2);

                LastUpdatedText = DateTime.Now.ToString("HH:mm:ss");

                OnPropertyChanged(nameof(LatestReport));

                _trackerMemoryHistory.Add(memMB);
                _gpuCacheMemoryHistory.Add(gpuMB);
                UpdateGraphPoints();
            }
            catch
            {
            }
        }

        private void UpdateGraphPoints()
        {
            const double graphWidth = 400.0;
            const double graphHeight = 100.0;

            var trackerData = _trackerMemoryHistory.ToArray();
            var gpuData = _gpuCacheMemoryHistory.ToArray();

            double max = 1.0;
            foreach (var v in trackerData) if (v > max) max = v;
            foreach (var v in gpuData) if (v > max) max = v;
            max = Math.Ceiling(max * 1.1);
            if (max < 1.0) max = 1.0;
            GraphMaxValue = max;

            TrackerGraphPoints = BuildPointCollection(trackerData, graphWidth, graphHeight, max);
            GpuCacheGraphPoints = BuildPointCollection(gpuData, graphWidth, graphHeight, max);
        }

        private static PointCollection BuildPointCollection(double[] data, double width, double height, double maxValue)
        {
            var points = new PointCollection();
            if (data.Length == 0) return points;

            double stepX = data.Length > 1 ? width / (data.Length - 1) : 0;
            for (int i = 0; i < data.Length; i++)
            {
                double x = i * stepX;
                double y = height - (data[i] / maxValue * height);
                points.Add(new Point(x, Math.Max(0, Math.Min(height, y))));
            }
            return points;
        }

        private void RefreshGpuCacheList()
        {
            try
            {
                var snapshot = GpuResourceCache.Instance.GetSnapshot();
                GpuCacheItems.Clear();
                foreach (var item in snapshot)
                {
                    item.EstimatedGpuMB = Math.Round(item.EstimatedGpuMB, 2);
                    GpuCacheItems.Add(item);
                }
            }
            catch
            {
            }
        }

        private static void CopyResourceListToClipboard(List<ResourceAllocation> resources, string title)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"== {title} ==");
            sb.AppendLine("Key\tType\tAge\tCreatedAt\tSize(B)\tStackTrace");
            foreach (var r in resources)
            {
                sb.AppendLine($"{r.Key}\t{r.ResourceType}\t{r.Age:hh\\:mm\\:ss}\t{r.CreatedAt:yyyy-MM-dd HH:mm:ss}\t{r.EstimatedSizeBytes}\t{r.StackTrace}");
            }
            Clipboard.SetText(sb.ToString());
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
                _dashboardTimer.Stop();
                _dashboardTimer.Tick -= OnDashboardTimerTick;
                ResourceAuditor.Instance.AuditCompleted -= _auditHandler;
            }
            catch
            {
            }
        }
    }
}