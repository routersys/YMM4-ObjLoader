using ObjLoader.Rendering.Shaders.Fx;
using ObjLoader.Rendering.Shaders.Interfaces;
using System.IO;

namespace ObjLoader.Rendering.Shaders;

public static class ShaderConverterFactory
{
    private static readonly HashSet<string> FxExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".fx", ".fxsub"
    };

    private static readonly HashSet<string> HlslExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".hlsl", ".shader", ".cg", ".glsl", ".vert", ".frag", ".txt"
    };

    public static bool IsSupported(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var ext = Path.GetExtension(filePath);
        return FxExtensions.Contains(ext) || HlslExtensions.Contains(ext);
    }

    public static IShaderConverter? CreateForFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var extension = Path.GetExtension(filePath);
        if (FxExtensions.Contains(extension)) return new FxShaderConverter();
        if (HlslExtensions.Contains(extension)) return new HlslShaderConverter();
        return null;
    }

    public static bool IsFxFormat(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return FxExtensions.Contains(Path.GetExtension(filePath));
    }

    public static bool IsHlslFormat(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return HlslExtensions.Contains(Path.GetExtension(filePath));
    }
}