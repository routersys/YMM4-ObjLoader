using ObjLoader.Rendering.Shaders.Fx;
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

    public static IShaderConverter CreateForFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var extension = Path.GetExtension(filePath);

        if (FxExtensions.Contains(extension))
        {
            return new FxShaderConverter();
        }

        return new HlslShaderConverter();
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
