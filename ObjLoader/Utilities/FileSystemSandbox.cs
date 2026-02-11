using System.Collections.Concurrent;
using System.IO;

namespace ObjLoader.Utilities
{
    public sealed class FileSystemSandbox
    {
        private static readonly Lazy<FileSystemSandbox> _instance = new Lazy<FileSystemSandbox>(() => new FileSystemSandbox());
        private readonly ConcurrentDictionary<string, byte> _allowedRoots = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private volatile bool _enforced = false;

        public static FileSystemSandbox Instance => _instance.Value;

        private FileSystemSandbox()
        {
        }

        public bool IsEnforced => _enforced;

        public void Enable()
        {
            _enforced = true;
        }

        public void Disable()
        {
            _enforced = false;
        }

        public void AddAllowedRoot(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath)) return;

            try
            {
                string normalized = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                _allowedRoots.TryAdd(normalized, 0);
            }
            catch
            {
            }
        }

        public void RemoveAllowedRoot(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath)) return;

            try
            {
                string normalized = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                _allowedRoots.TryRemove(normalized, out _);
            }
            catch
            {
            }
        }

        public void ClearAllowedRoots()
        {
            _allowedRoots.Clear();
        }

        public IReadOnlyCollection<string> GetAllowedRoots()
        {
            return _allowedRoots.Keys.ToArray();
        }

        public bool IsPathAllowed(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            if (!_enforced) return true;

            if (_allowedRoots.IsEmpty) return true;

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return false;
            }

            foreach (var root in _allowedRoots.Keys)
            {
                if (IsSubPathOf(normalizedPath, root))
                {
                    return true;
                }
            }

            return false;
        }

        public SandboxValidationResult ValidatePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return SandboxValidationResult.Rejected("Path is null or empty.");

            var pathValidation = PathValidator.Validate(path);
            if (!pathValidation.IsValid)
                return SandboxValidationResult.Rejected(pathValidation.ErrorMessage ?? "Path validation failed.");

            string normalizedPath = pathValidation.NormalizedPath!;

            if (_enforced && !_allowedRoots.IsEmpty)
            {
                bool inSandbox = false;
                foreach (var root in _allowedRoots.Keys)
                {
                    if (IsSubPathOf(normalizedPath, root))
                    {
                        inSandbox = true;
                        break;
                    }
                }

                if (!inSandbox)
                    return SandboxValidationResult.Rejected("Path is outside allowed directories.");
            }

            try
            {
                if (File.Exists(normalizedPath))
                {
                    var attrs = File.GetAttributes(normalizedPath);
                    if ((attrs & FileAttributes.ReparsePoint) != 0)
                    {
                        string? target = ResolveSymlink(normalizedPath);
                        if (target != null)
                        {
                            var targetValidation = PathValidator.Validate(target);
                            if (!targetValidation.IsValid)
                                return SandboxValidationResult.Rejected("Symlink target fails validation.");

                            if (_enforced && !_allowedRoots.IsEmpty)
                            {
                                bool targetInSandbox = false;
                                foreach (var root in _allowedRoots.Keys)
                                {
                                    if (IsSubPathOf(targetValidation.NormalizedPath!, root))
                                    {
                                        targetInSandbox = true;
                                        break;
                                    }
                                }

                                if (!targetInSandbox)
                                    return SandboxValidationResult.Rejected("Symlink target is outside allowed directories.");
                            }

                            return SandboxValidationResult.Accepted(targetValidation.NormalizedPath!);
                        }
                    }
                }
            }
            catch
            {
                return SandboxValidationResult.Rejected("Failed to check file attributes.");
            }

            return SandboxValidationResult.Accepted(normalizedPath);
        }

        private static bool IsSubPathOf(string path, string root)
        {
            string normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return true;

            string rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
            return normalizedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string? ResolveSymlink(string path)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.LinkTarget != null)
                {
                    string resolved = Path.IsPathRooted(fileInfo.LinkTarget)
                        ? fileInfo.LinkTarget
                        : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path) ?? "", fileInfo.LinkTarget));
                    return Path.GetFullPath(resolved);
                }

                return Path.GetFullPath(path);
            }
            catch
            {
                return null;
            }
        }

        public readonly struct SandboxValidationResult
        {
            public bool IsAllowed { get; }
            public string? ResolvedPath { get; }
            public string? RejectionReason { get; }

            private SandboxValidationResult(bool isAllowed, string? resolvedPath, string? rejectionReason)
            {
                IsAllowed = isAllowed;
                ResolvedPath = resolvedPath;
                RejectionReason = rejectionReason;
            }

            public static SandboxValidationResult Accepted(string resolvedPath)
            {
                return new SandboxValidationResult(true, resolvedPath, null);
            }

            public static SandboxValidationResult Rejected(string reason)
            {
                return new SandboxValidationResult(false, null, reason);
            }
        }
    }
}