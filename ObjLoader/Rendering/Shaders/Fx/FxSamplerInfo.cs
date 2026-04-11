namespace ObjLoader.Rendering.Shaders.Fx;

internal sealed record FxSamplerInfo(string Name, string? TextureName)
{
    public int Slot { get; set; } = -1;
}