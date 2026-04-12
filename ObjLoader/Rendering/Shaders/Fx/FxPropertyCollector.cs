using System.Text.RegularExpressions;

namespace ObjLoader.Rendering.Shaders.Fx;

internal sealed partial class FxPropertyCollector
{
    [GeneratedRegex(@"(?:texture2D|texture)\s+(\w+)\s*:\s*(\w+)\s*(?:<[^>]*>)?\s*;", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TextureSemanticPattern();

    [GeneratedRegex(@"(?:sampler2D|sampler)\s+(\w+)\s*=\s*sampler_state\s*\{([^}]*)\}\s*;", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SamplerStatePattern();

    [GeneratedRegex(@"texture\s*=\s*<(\w+)>", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TextureRefInSamplerPattern();

    [GeneratedRegex(@"VertexShader\s*=\s*compile\s+\w+\s+(\w+)\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex VsEntryPointInTechniquePattern();

    [GeneratedRegex(@"PixelShader\s*=\s*compile\s+\w+\s+(\w+)\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PsEntryPointInTechniquePattern();

    [GeneratedRegex(@"\b(\w+)\s+(\w+)\s*\(", RegexOptions.Compiled)]
    private static partial Regex FunctionSignaturePattern();

    private static readonly HashSet<string> MainTextureSemantics = new(StringComparer.OrdinalIgnoreCase)
    {
        "MATERIALTEXTURE", "TEXTURE"
    };

    private static readonly HashSet<string> RemovedTextureSemantics = new(StringComparer.OrdinalIgnoreCase)
    {
        "SHADOWMAP", "RENDERCOLORTARGET", "OFFSCREENRENDERTARGET", "RENDERDEPTHSTENCILTARGET"
    };

    private static readonly HashSet<string> MmdSystemSamplerNames = new(StringComparer.Ordinal)
    {
        "DefSampler", "ScreenShadowMapProcessedSamp", "ExShadowSSAOMapSamp"
    };

    public FxCollectedProperties Collect(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var props = new FxCollectedProperties();
        CollectTextures(source, props);
        CollectSamplers(source, props);
        CollectEntryPoints(source, props);
        CollectVsOutputType(source, props);
        AssignSlots(props);
        return props;
    }

    private static void CollectEntryPoints(string source, FxCollectedProperties props)
    {
        var vsMatch = VsEntryPointInTechniquePattern().Match(source);
        if (vsMatch.Success)
            props.VsEntryPoint = vsMatch.Groups[1].Value;

        var psMatch = PsEntryPointInTechniquePattern().Match(source);
        if (psMatch.Success)
            props.PsEntryPoint = psMatch.Groups[1].Value;
    }

    private static void CollectVsOutputType(string source, FxCollectedProperties props)
    {
        if (string.IsNullOrEmpty(props.VsEntryPoint)) return;

        foreach (Match m in FunctionSignaturePattern().Matches(source))
        {
            if (string.Equals(m.Groups[2].Value, props.VsEntryPoint, StringComparison.OrdinalIgnoreCase))
            {
                var type = m.Groups[1].Value;
                if (!string.Equals(type, "return", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(type, "void", StringComparison.OrdinalIgnoreCase))
                {
                    props.VsOutputType = type;
                    return;
                }
            }
        }
    }

    private static void CollectTextures(string source, FxCollectedProperties props)
    {
        foreach (Match m in TextureSemanticPattern().Matches(source))
        {
            props.AddTexture(m.Groups[1].Value, m.Groups[2].Value);
        }
    }

    private static void CollectSamplers(string source, FxCollectedProperties props)
    {
        foreach (Match m in SamplerStatePattern().Matches(source))
        {
            var name = m.Groups[1].Value;
            var body = m.Groups[2].Value;
            var texMatch = TextureRefInSamplerPattern().Match(body);
            var textureName = texMatch.Success ? texMatch.Groups[1].Value : null;

            if (MmdSystemSamplerNames.Contains(name))
            {
                props.AddSampler(name, null);
                continue;
            }

            props.AddSampler(name, textureName);
        }
    }

    private static void AssignSlots(FxCollectedProperties props)
    {
        var textureSlot = 1;
        foreach (var (_, tex) in props.Textures)
        {
            if (MainTextureSemantics.Contains(tex.Semantic))
            {
                tex.Slot = 0;
            }
            else if (RemovedTextureSemantics.Contains(tex.Semantic))
            {
                tex.Slot = -1;
                props.IsPostEffect = true;
            }
            else
            {
                tex.Slot = textureSlot++;
            }
        }

        var samplerSlot = 1;
        foreach (var (_, sam) in props.Samplers)
        {
            if (sam.TextureName is null)
            {
                sam.Slot = -1;
                continue;
            }

            if (!props.Textures.TryGetValue(sam.TextureName, out var tex))
            {
                sam.Slot = -1;
                continue;
            }

            sam.Slot = tex.Slot switch
            {
                < 0 => -1,
                0 => 0,
                _ => samplerSlot++
            };
        }
    }
}