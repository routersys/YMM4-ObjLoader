using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Shaders;

internal sealed class CompiledShaderSet : IDisposable
{
    public ID3D11VertexShader? VertexShader { get; }
    public ID3D11PixelShader? PixelShader { get; }
    public ID3D11GeometryShader? GeometryShader { get; }
    public ID3D11HullShader? HullShader { get; }
    public ID3D11DomainShader? DomainShader { get; }
    public ID3D11ComputeShader? ComputeShader { get; }
    public ID3D11InputLayout? InputLayout { get; }

    public bool HasAny =>
        VertexShader is not null ||
        PixelShader is not null ||
        GeometryShader is not null ||
        HullShader is not null ||
        DomainShader is not null ||
        ComputeShader is not null;

    public CompiledShaderSet(
        ID3D11VertexShader? vertexShader,
        ID3D11PixelShader? pixelShader,
        ID3D11GeometryShader? geometryShader,
        ID3D11HullShader? hullShader,
        ID3D11DomainShader? domainShader,
        ID3D11ComputeShader? computeShader,
        ID3D11InputLayout? inputLayout)
    {
        VertexShader = vertexShader;
        PixelShader = pixelShader;
        GeometryShader = geometryShader;
        HullShader = hullShader;
        DomainShader = domainShader;
        ComputeShader = computeShader;
        InputLayout = inputLayout;
    }

    public void Dispose()
    {
        VertexShader?.Dispose();
        PixelShader?.Dispose();
        GeometryShader?.Dispose();
        HullShader?.Dispose();
        DomainShader?.Dispose();
        ComputeShader?.Dispose();
        InputLayout?.Dispose();
    }
}