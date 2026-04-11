namespace ObjLoader.Rendering.Shaders.Fx;

internal sealed record FxTextureInfo(string Name, string Semantic)
{
    public int Slot { get; set; } = -1;
}