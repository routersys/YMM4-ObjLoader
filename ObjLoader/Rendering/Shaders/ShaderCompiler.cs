using ObjLoader.Rendering.Shaders.Interfaces;
using Vortice.D3DCompiler;
using Vortice.Direct3D11;
using Vortice.Direct3D11.Shader;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Rendering.Shaders;

internal sealed class ShaderCompiler : IShaderCompiler
{
    private readonly IGraphicsDevicesAndContext _devices;

    internal ShaderCompiler(IGraphicsDevicesAndContext devices)
    {
        _devices = devices;
    }

    public CompiledShaderSet? Compile(string source)
    {
        ID3D11VertexShader? vs = null;
        ID3D11PixelShader? ps = null;
        ID3D11GeometryShader? gs = null;
        ID3D11HullShader? hs = null;
        ID3D11DomainShader? ds = null;
        ID3D11ComputeShader? cs = null;
        ID3D11InputLayout? il = null;

        try
        {
            var vsResult = ShaderStore.Compile(source, "VS", "vs_5_0");
            if (vsResult.ByteCode != null)
            {
                vs = _devices.D3D.Device.CreateVertexShader(vsResult.ByteCode);
                il = CreateInputLayout(vsResult.ByteCode);
            }

            var psResult = ShaderStore.Compile(source, "PS", "ps_5_0");
            if (psResult.ByteCode != null)
                ps = _devices.D3D.Device.CreatePixelShader(psResult.ByteCode);

            var gsResult = ShaderStore.Compile(source, "GS", "gs_5_0");
            if (gsResult.ByteCode != null)
                gs = _devices.D3D.Device.CreateGeometryShader(gsResult.ByteCode);

            var hsResult = ShaderStore.Compile(source, "HS", "hs_5_0");
            if (hsResult.ByteCode != null)
                hs = _devices.D3D.Device.CreateHullShader(hsResult.ByteCode);

            var dsResult = ShaderStore.Compile(source, "DS", "ds_5_0");
            if (dsResult.ByteCode != null)
                ds = _devices.D3D.Device.CreateDomainShader(dsResult.ByteCode);

            var csResult = ShaderStore.Compile(source, "CS", "cs_5_0");
            if (csResult.ByteCode != null)
                cs = _devices.D3D.Device.CreateComputeShader(csResult.ByteCode);
        }
        catch
        {
            vs?.Dispose(); ps?.Dispose(); gs?.Dispose();
            hs?.Dispose(); ds?.Dispose(); cs?.Dispose(); il?.Dispose();
            return null;
        }

        var compiled = new CompiledShaderSet(vs, ps, gs, hs, ds, cs, il);
        return compiled.HasAny ? compiled : null;
    }

    private ID3D11InputLayout? CreateInputLayout(byte[] vertexShaderBytecode)
    {
        try
        {
            using var reflection = Compiler.Reflect<ID3D11ShaderReflection>(vertexShaderBytecode);
            var desc = reflection.Description;
            var elements = new List<InputElementDescription>();
            var byteOffset = 0;

            for (var i = 0; i < desc.InputParameters; i++)
            {
                var paramDesc = reflection.GetInputParameterDescription(i);

                var semanticName = string.Equals(paramDesc.SemanticName, "SV_Position", StringComparison.OrdinalIgnoreCase)
                    ? "POSITION"
                    : paramDesc.SemanticName;

                var format = semanticName.ToUpperInvariant() switch
                {
                    "POSITION" => Format.R32G32B32_Float,
                    "NORMAL" => Format.R32G32B32_Float,
                    "TEXCOORD" => Format.R32G32_Float,
                    _ => DetermineFormat(paramDesc.UsageMask, paramDesc.ComponentType)
                };

                elements.Add(new InputElementDescription(
                    semanticName,
                    (int)paramDesc.SemanticIndex,
                    format,
                    byteOffset,
                    0));

                byteOffset += FormatByteSize(format);
            }

            if (elements.Count == 0) return null;
            return _devices.D3D.Device.CreateInputLayout(elements.ToArray(), vertexShaderBytecode);
        }
        catch
        {
            var fallbackElements = new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32B32_Float,  0, 0),
                new InputElementDescription("NORMAL",   0, Format.R32G32B32_Float, 12, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float,    24, 0)
            };
            return _devices.D3D.Device.CreateInputLayout(fallbackElements, vertexShaderBytecode);
        }
    }

    private static Format DetermineFormat(RegisterComponentMaskFlags mask, Vortice.Direct3D.RegisterComponentType componentType)
    {
        var componentCount = 0;
        if (mask.HasFlag(RegisterComponentMaskFlags.ComponentX)) componentCount++;
        if (mask.HasFlag(RegisterComponentMaskFlags.ComponentY)) componentCount++;
        if (mask.HasFlag(RegisterComponentMaskFlags.ComponentZ)) componentCount++;
        if (mask.HasFlag(RegisterComponentMaskFlags.ComponentW)) componentCount++;

        return componentType switch
        {
            Vortice.Direct3D.RegisterComponentType.Float32 => componentCount switch
            {
                1 => Format.R32_Float,
                2 => Format.R32G32_Float,
                3 => Format.R32G32B32_Float,
                _ => Format.R32G32B32A32_Float
            },
            Vortice.Direct3D.RegisterComponentType.UInt32 => componentCount switch
            {
                1 => Format.R32_UInt,
                2 => Format.R32G32_UInt,
                3 => Format.R32G32B32_UInt,
                _ => Format.R32G32B32A32_UInt
            },
            Vortice.Direct3D.RegisterComponentType.SInt32 => componentCount switch
            {
                1 => Format.R32_SInt,
                2 => Format.R32G32_SInt,
                3 => Format.R32G32B32_SInt,
                _ => Format.R32G32B32A32_SInt
            },
            _ => Format.R32G32B32A32_Float
        };
    }

    private static int FormatByteSize(Format format) => format switch
    {
        Format.R32_Float or Format.R32_UInt or Format.R32_SInt => 4,
        Format.R32G32_Float or Format.R32G32_UInt or Format.R32G32_SInt => 8,
        Format.R32G32B32_Float or Format.R32G32B32_UInt or Format.R32G32B32_SInt => 12,
        _ => 16
    };
}