using ObjLoader.Cache.Core;
using ObjLoader.Localization;
using ObjLoader.Utilities;
using ObjLoader.Utilities.Logging;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels.Settings
{
    public class CacheEntryViewModel : Bindable
    {
        private string _originalPath = string.Empty;
        public string OriginalPath
        {
            get => _originalPath;
            set => Set(ref _originalPath, value);
        }

        private string _cachePath = string.Empty;
        public string CachePath
        {
            get => _cachePath;
            set => Set(ref _cachePath, value);
        }

        private long _totalSizeBytes;
        public long TotalSizeBytes
        {
            get => _totalSizeBytes;
            set
            {
                if (Set(ref _totalSizeBytes, value))
                {
                    OnPropertyChanged(nameof(TotalSizeText));
                }
            }
        }

        public string TotalSizeText => $"{TotalSizeBytes / (1024.0 * 1024.0):F2} MB";

        private DateTime _lastAccess;
        public DateTime LastAccess
        {
            get => _lastAccess;
            set => Set(ref _lastAccess, value);
        }

        private bool _isSplit;
        public bool IsSplit
        {
            get => _isSplit;
            set
            {
                if (Set(ref _isSplit, value))
                {
                    OnPropertyChanged(nameof(TypeText));
                }
            }
        }

        public string TypeText => IsSplit ? Texts.CacheTypeHddSplit : Texts.CacheTypeSsdSingle;

        private int _partsCount;
        public int PartsCount
        {
            get => _partsCount;
            set => Set(ref _partsCount, value);
        }

        public ICommand ConvertCommand { get; }

        public CacheEntryViewModel()
        {
            ConvertCommand = new ActionCommand(_ => true, _ =>
            {
                try
                {
                    bool targetSplit = !IsSplit;
                    CacheManager.ConvertCache(OriginalPath, targetSplit);

                    IsSplit = targetSplit;

                    var index = ObjLoader.Settings.ModelSettings.Instance.GetCacheIndex();
                    if (index.Entries.TryGetValue(OriginalPath, out var entry))
                    {
                        TotalSizeBytes = entry.TotalSize;
                        PartsCount = entry.PartsCount;
                        LastAccess = entry.LastAccessTime;
                    }
                }
                catch (Exception ex)
                {
                    Logger<CacheEntryViewModel>.Instance.Error($"Conversion failed for '{OriginalPath}'", ex);
                    UserNotification.ShowWarning(string.Format(Texts.CacheConvertFailed, ex.Message), Texts.ErrorTitle);
                }
            });
        }
    }
}
