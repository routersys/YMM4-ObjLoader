namespace ObjLoader.Rendering.Shaders.Interfaces;

internal interface IShaderCompiler
{
    CompiledShaderSet? Compile(string source);
}