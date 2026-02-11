using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ObjLoader.Utilities
{
    public static class PathValidator
    {
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".obj", ".mtl", ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".tiff", ".tif", ".gif", ".dds", ".hdr", ".exr", ".webp",
            ".pmx", ".pmd", ".stl", ".glb", ".gltf", ".ply", ".3mf", ".dae", ".fbx", ".x", ".3ds", ".dxf",
            ".ifc", ".lwo", ".lws", ".lxo", ".ac", ".ms3d", ".cob", ".scn", ".bvh", ".mdl", ".md2", ".md3",
            ".pk3", ".mdc", ".md5mesh", ".smd", ".vta", ".ogex", ".3d", ".b3d", ".q3d", ".q3s", ".nff",
            ".off", ".raw", ".ter", ".hmp", ".ndo", ".xgl", ".zgl", ".xml", ".ase", ".blend", ".bin",
            ".hlsl", ".fx", ".shader", ".cg", ".glsl", ".vert", ".frag", ".txt"
        };

        private static readonly HashSet<string> ReservedDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private static readonly Regex TraversalPattern = new Regex(
            @"(^|[\\/])\.\.($|[\\/])",
            RegexOptions.Compiled);

        private static readonly Regex NullBytePattern = new Regex(
            @"\x00",
            RegexOptions.Compiled);

        public static ValidationResult Validate(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return ValidationResult.Fail("Path is null or empty.");

            if (NullBytePattern.IsMatch(path))
                return ValidationResult.Fail("Path contains null bytes.");

            if (path.Length > 32767)
                return ValidationResult.Fail("Path exceeds maximum length.");

            string trimmed = path.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(trimmed))
                return ValidationResult.Fail("Path is empty after trimming.");

            if (trimmed.StartsWith(@"\\?\") || trimmed.StartsWith(@"\\.\"))
                return ValidationResult.Fail("Extended-length path prefixes are not allowed.");

            if (IsUncPath(trimmed))
                return ValidationResult.Fail("UNC paths are not allowed.");

            if (TraversalPattern.IsMatch(trimmed))
                return ValidationResult.Fail("Path traversal sequences detected.");

            string? normalized;
            try
            {
                normalized = NormalizePath(trimmed);
            }
            catch
            {
                return ValidationResult.Fail("Path normalization failed.");
            }

            if (string.IsNullOrEmpty(normalized))
                return ValidationResult.Fail("Normalized path is empty.");

            if (TraversalPattern.IsMatch(normalized))
                return ValidationResult.Fail("Path traversal detected after normalization.");

            if (IsUncPath(normalized))
                return ValidationResult.Fail("Normalized path resolves to UNC path.");

            string fileName = Path.GetFileNameWithoutExtension(normalized);
            if (!string.IsNullOrEmpty(fileName) && ReservedDeviceNames.Contains(fileName))
                return ValidationResult.Fail("Path contains reserved device name.");

            string extension = Path.GetExtension(normalized);
            if (string.IsNullOrEmpty(extension))
                return ValidationResult.Fail("Path has no file extension.");

            if (!AllowedExtensions.Contains(extension))
                return ValidationResult.Fail($"Extension '{extension}' is not allowed.");

            foreach (char c in Path.GetInvalidPathChars())
            {
                if (normalized.Contains(c))
                    return ValidationResult.Fail("Path contains invalid characters.");
            }

            string fileNameFull = Path.GetFileName(normalized);
            if (!string.IsNullOrEmpty(fileNameFull))
            {
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar || c == ':')
                        continue;
                    if (fileNameFull.Contains(c))
                        return ValidationResult.Fail("Filename contains invalid characters.");
                }
            }

            return ValidationResult.Success(normalized);
        }

        public static bool IsAllowedExtension(string? path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path);
            return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
        }

        public static string? NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            string full = Path.GetFullPath(path);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    if (File.Exists(full))
                    {
                        var fileInfo = new FileInfo(full);
                        string? dir = fileInfo.DirectoryName;
                        if (dir != null)
                        {
                            var dirInfo = new DirectoryInfo(dir);
                            string realDir = dirInfo.FullName;
                            full = Path.Combine(realDir, fileInfo.Name);
                        }
                    }
                }
                catch
                {
                }
            }

            return full;
        }

        public static string? SafeNormalize(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var result = Validate(path);
            return result.IsValid ? result.NormalizedPath : null;
        }

        private static bool IsUncPath(string path)
        {
            return path.StartsWith(@"\\") || path.StartsWith("//");
        }

        public readonly struct ValidationResult
        {
            public bool IsValid { get; }
            public string? NormalizedPath { get; }
            public string? ErrorMessage { get; }

            private ValidationResult(bool isValid, string? normalizedPath, string? errorMessage)
            {
                IsValid = isValid;
                NormalizedPath = normalizedPath;
                ErrorMessage = errorMessage;
            }

            public static ValidationResult Success(string normalizedPath)
            {
                return new ValidationResult(true, normalizedPath, null);
            }

            public static ValidationResult Fail(string errorMessage)
            {
                return new ValidationResult(false, null, errorMessage);
            }
        }
    }
}