namespace ObjLoader.Rendering.Shaders.Interfaces;

internal interface IShaderCache
{
    CompiledShaderSet? Resolve(string? path);
}