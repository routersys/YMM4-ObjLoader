using ObjLoader.Localization;
using ObjLoader.Settings;
using ObjLoader.Utilities;
using ObjLoader.Utilities.Logging;
using System.IO;

namespace ObjLoader.Cache.Core
{
    public static class CacheManager
    {
        private static readonly Logger _logger = LoggerFactory.CreateLogger(nameof(CacheManager));

        private const string CacheDirName = ".cache";

        public static void DeleteCache(string originalPath)
        {
            var index = ModelSettings.Instance.GetCacheIndex();
            if (index.Entries.TryGetValue(originalPath, out var entry))
            {
                bool fileSystemSuccess = false;
                try
                {
                    string root = Path.GetDirectoryName(originalPath) ?? string.Empty;
                    string cacheDir = Path.Combine(root, CacheDirName, entry.ModelHash);

                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                    }
                    else
                    {
                        string legacyPath = originalPath + ".bin";
                        if (File.Exists(legacyPath))
                        {
                            File.Delete(legacyPath);
                        }
                    }
                    fileSystemSuccess = true;
                }
                catch (IOException ex)
                {
                    _logger.Error($"Failed to delete files for '{originalPath}'", ex);
                    UserNotification.ShowWarning(string.Format(Texts.CacheDeleteFailed, originalPath), Texts.ErrorTitle);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.Error($"Access denied deleting files for '{originalPath}'", ex);
                    UserNotification.ShowWarning(string.Format(Texts.CacheDeleteFailed, originalPath), Texts.ErrorTitle);
                }

                if (fileSystemSuccess)
                {
                    index.Entries.Remove(originalPath);
                    ModelSettings.Instance.SaveCacheIndex(index);
                }
            }
            else
            {
                try
                {
                    string legacyPath = originalPath + ".bin";
                    if (File.Exists(legacyPath))
                    {
                        File.Delete(legacyPath);
                    }
                }
                catch (IOException ex)
                {
                    _logger.Warning($"Failed to delete legacy file for '{originalPath}'", ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.Warning($"Access denied for legacy file '{originalPath}'", ex);
                }
            }
        }

        private static readonly object _cleanupLock = new object();

        public static void CleanUpCache()
        {
            lock (_cleanupLock)
            {
                var index = ModelSettings.Instance.GetCacheIndex();
                var keysToRemove = new List<string>();

                foreach (var kvp in index.Entries)
                {
                    try
                    {
                        if (!File.Exists(kvp.Key))
                        {
                            keysToRemove.Add(kvp.Key);
                            string root = Path.GetDirectoryName(kvp.Key) ?? string.Empty;
                            string cacheDir = Path.Combine(root, CacheDirName, kvp.Value.ModelHash);
                            if (Directory.Exists(cacheDir))
                            {
                                Directory.Delete(cacheDir, true);
                            }
                        }
                    }
                    catch (IOException ex)
                    {
                        _logger.Warning($"Failed to clean entry '{kvp.Key}'", ex);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.Warning($"Access denied cleaning entry '{kvp.Key}'", ex);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    index.Entries.Remove(key);
                }

                ModelSettings.Instance.SaveCacheIndex(index);
            }
        }

        public static long GetTotalCacheSize()
        {
            var index = ModelSettings.Instance.GetCacheIndex();
            long total = 0;
            foreach (var entry in index.Entries.Values)
            {
                total += entry.TotalSize;
            }
            return total;
        }

        public static void MoveCache(string oldRoot, string newRoot)
        {
            var index = ModelSettings.Instance.GetCacheIndex();
            var updates = new List<(string OldKey, CacheIndex.CacheEntry Entry)>();

            foreach (var key in index.Entries.Keys)
            {
                if (key.StartsWith(oldRoot, StringComparison.OrdinalIgnoreCase))
                {
                    updates.Add((key, index.Entries[key]));
                }
            }

            var completedMoves = new List<(string OldKey, string NewKey, string OldHash, string NewHash, CacheIndex.CacheEntry Entry)>();

            foreach (var (oldKey, entry) in updates)
            {
                string relativePart = oldKey.Substring(oldRoot.Length);
                if (relativePart.StartsWith("\\") || relativePart.StartsWith("/")) relativePart = relativePart.Substring(1);
                string newPath = Path.Combine(newRoot, relativePart);

                string oldHash = entry.ModelHash;
                string newHash = ComputeModelHash(newPath);

                try
                {
                    string rootDir = Path.GetDirectoryName(newPath) ?? string.Empty;
                    string cacheDir = Path.Combine(rootDir, CacheDirName);
                    string oldDir = Path.Combine(cacheDir, oldHash);
                    string newDir = Path.Combine(cacheDir, newHash);

                    if (Directory.Exists(oldDir) && oldDir != newDir)
                    {
                        if (Directory.Exists(newDir)) Directory.Delete(newDir, true);
                        Directory.Move(oldDir, newDir);
                    }

                    index.Entries.Remove(oldKey);
                    entry.OriginalPath = newPath;
                    entry.ModelHash = newHash;
                    entry.CacheRootPath = Path.Combine(CacheDirName, newHash);
                    index.Entries[newPath] = entry;

                    completedMoves.Add((oldKey, newPath, oldHash, newHash, entry));
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to move '{oldKey}' -> '{newPath}'", ex);

                    try
                    {
                        for (int i = completedMoves.Count - 1; i >= 0; i--)
                        {
                            var (rollbackOldKey, rollbackNewKey, rollbackOldHash, rollbackNewHash, rollbackEntry) = completedMoves[i];
                            index.Entries.Remove(rollbackNewKey);
                            rollbackEntry.OriginalPath = rollbackOldKey;
                            rollbackEntry.ModelHash = rollbackOldHash;
                            rollbackEntry.CacheRootPath = Path.Combine(CacheDirName, rollbackOldHash);
                            index.Entries[rollbackOldKey] = rollbackEntry;

                            string rootDir = Path.GetDirectoryName(rollbackOldKey) ?? string.Empty;
                            string cacheDir = Path.Combine(rootDir, CacheDirName);
                            string movedDir = Path.Combine(cacheDir, rollbackNewHash);
                            string originalDir = Path.Combine(cacheDir, rollbackOldHash);

                            if (Directory.Exists(movedDir) && movedDir != originalDir)
                            {
                                if (Directory.Exists(originalDir)) Directory.Delete(originalDir, true);
                                Directory.Move(movedDir, originalDir);
                            }
                        }
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.Error("Rollback failed", rollbackEx);
                        UserNotification.ShowWarning(string.Format(Texts.CacheMoveRollbackFailed, rollbackEx.Message), Texts.ErrorTitle);
                    }

                    UserNotification.ShowWarning(string.Format(Texts.CacheMoveFailed, ex.Message), Texts.ErrorTitle);
                    break;
                }
            }

            ModelSettings.Instance.SaveCacheIndex(index);
        }

        public static void ConvertCache(string path, bool toSplit)
        {
            var cache = new ModelCache();
            cache.Convert(path, toSplit);
        }

        private static string ComputeModelHash(string path)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(path.ToLowerInvariant());
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}