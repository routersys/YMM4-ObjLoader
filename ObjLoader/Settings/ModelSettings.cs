using ObjLoader.Infrastructure;
using ObjLoader.Localization;
using ObjLoader.Utilities;
using YukkuriMovieMaker.Plugin;

namespace ObjLoader.Settings
{
    public class ModelSettings : SettingsBase<ModelSettings>
    {
        public override string Name => Texts.Settings_3DModel;
        public override SettingsCategory Category => SettingsCategory.Shape;
        public override bool HasSettingView => true;
        public override object SettingView => new Views.ModelSettingsView { DataContext = new ViewModels.ModelSettingsViewModel(this) };
        public static ModelSettings Instance => Default;

        private bool _isSandboxEnforced = false;
        public bool IsSandboxEnforced
        {
            get => _isSandboxEnforced;
            set => Set(ref _isSandboxEnforced, value);
        }

        private List<string> _allowedRoots = new List<string>();
        public List<string> AllowedRoots
        {
            get => _allowedRoots;
            set => Set(ref _allowedRoots, value ?? new List<string>());
        }

        private bool _enableAutoAudit = false;
        public bool EnableAutoAudit
        {
            get => _enableAutoAudit;
            set => Set(ref _enableAutoAudit, value);
        }

        private double _auditIntervalMinutes = 5.0;
        public double AuditIntervalMinutes
        {
            get => _auditIntervalMinutes;
            set => Set(ref _auditIntervalMinutes, Math.Max(0.5, value));
        }

        private double _leakThresholdMinutes = 30.0;
        public double LeakThresholdMinutes
        {
            get => _leakThresholdMinutes;
            set => Set(ref _leakThresholdMinutes, Math.Max(1.0, value));
        }

        public override void Initialize()
        {
            try
            {
                if (_isSandboxEnforced)
                    FileSystemSandbox.Instance.Enable();
                else
                    FileSystemSandbox.Instance.Disable();

                FileSystemSandbox.Instance.ClearAllowedRoots();
                if (_allowedRoots != null)
                {
                    foreach (var root in _allowedRoots)
                    {
                        if (!string.IsNullOrWhiteSpace(root))
                            FileSystemSandbox.Instance.AddAllowedRoot(root);
                    }
                }

                ResourceAuditor.Instance.SetLeakThreshold(TimeSpan.FromMinutes(Math.Max(1.0, _leakThresholdMinutes)));

                if (_enableAutoAudit)
                {
                    ResourceAuditor.Instance.Start(TimeSpan.FromMinutes(Math.Max(0.5, _auditIntervalMinutes)));
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
    }
}