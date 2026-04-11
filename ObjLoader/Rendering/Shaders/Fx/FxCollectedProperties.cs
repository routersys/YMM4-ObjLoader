namespace ObjLoader.Rendering.Shaders.Fx;

internal sealed class FxCollectedProperties
{
    private readonly Dictionary<string, FxTextureInfo> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FxSamplerInfo> _samplers = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, FxTextureInfo> Textures => _textures;
    public IReadOnlyDictionary<string, FxSamplerInfo> Samplers => _samplers;

    public string VsEntryPoint { get; internal set; } = string.Empty;
    public string PsEntryPoint { get; internal set; } = string.Empty;
    public string VsOutputType { get; internal set; } = string.Empty;
    public bool IsPostEffect { get; internal set; }

    internal void AddTexture(string name, string semantic) =>
        _textures.TryAdd(name, new FxTextureInfo(name, semantic));

    internal void AddSampler(string name, string? textureName) =>
        _samplers.TryAdd(name, new FxSamplerInfo(name, textureName));

    public string? ResolveTextureName(string samplerName) =>
        _samplers.TryGetValue(samplerName, out var sam) && sam.TextureName is not null &&
        _textures.TryGetValue(sam.TextureName, out var tex)
            ? tex.Name
            : null;

    public bool IsMmdSystemSampler(string samplerName) =>
        _samplers.TryGetValue(samplerName, out var sam) && sam.Slot < 0 && sam.TextureName is null;
}