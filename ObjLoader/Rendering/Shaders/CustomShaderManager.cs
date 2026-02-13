using ObjLoader.Plugin;
using System.Security.Cryptography;
using System.Text;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Direct3D11.Shader;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Rendering.Shaders
{
    internal class CustomShaderManager : IDisposable
    {
        private readonly IGraphicsDevicesAndContext _devices;
        private string _loadedShaderPath = string.Empty;
        private string _compiledSourceHash = string.Empty;

        public ID3D11VertexShader? VertexShader { get; private set; }
        public ID3D11PixelShader? PixelShader { get; private set; }
        public ID3D11GeometryShader? GeometryShader { get; private set; }
        public ID3D11HullShader? HullShader { get; private set; }
        public ID3D11DomainShader? DomainShader { get; private set; }
        public ID3D11ComputeShader? ComputeShader { get; private set; }
        public ID3D11InputLayout? InputLayout { get; private set; }

        public CustomShaderManager(IGraphicsDevicesAndContext devices)
        {
            _devices = devices;
        }

        public void Update(string path, ObjLoaderParameter parameter)
        {
            if (string.IsNullOrEmpty(path))
            {
                if (_loadedShaderPath != string.Empty)
                {
                    Dispose();
                    _loadedShaderPath = string.Empty;
                    _compiledSourceHash = string.Empty;
                }
                return;
            }

            var source = parameter.GetAdaptedShaderSource();
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            var sourceHash = ComputeHash(source);
            if (path == _loadedShaderPath && sourceHash == _compiledSourceHash && VertexShader != null)
            {
                return;
            }

            Dispose();
            _loadedShaderPath = path;
            _compiledSourceHash = sourceHash;

            try
            {
                CompileVertexShader(source);
                CompilePixelShader(source);
                CompileGeometryShader(source);
                CompileHullShader(source);
                CompileDomainShader(source);
                CompileComputeShader(source);
            }
            catch
            {
            }
        }

        private void CompileVertexShader(string source)
        {
            var vsResult = ShaderStore.Compile(source, "VS", "vs_5_0");
            if (vsResult.ByteCode == null) return;

            VertexShader = _devices.D3D.Device.CreateVertexShader(vsResult.ByteCode);
            InputLayout = CreateInputLayoutFromReflection(vsResult.ByteCode);
        }

        private ID3D11InputLayout? CreateInputLayoutFromReflection(byte[] vsByteCode)
        {
            try
            {
                using var reflection = Compiler.Reflect<ID3D11ShaderReflection>(vsByteCode);
                var desc = reflection.Description;
                var elements = new List<InputElementDescription>();
                var offset = 0;

                for (var i = 0; i < desc.InputParameters; i++)
                {
                    var paramDesc = reflection.GetInputParameterDescription(i);
                    var format = DetermineFormat(paramDesc.UsageMask, paramDesc.ComponentType);
                    var size = GetFormatSize(format);

                    elements.Add(new InputElementDescription(
                        paramDesc.SemanticName,
                        (int)paramDesc.SemanticIndex,
                        format,
                        offset,
                        0));

                    offset += size;
                }

                if (elements.Count == 0) return null;
                return _devices.D3D.Device.CreateInputLayout(elements.ToArray(), vsByteCode);
            }
            catch
            {
                var inputElements = new[]
                {
                    new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                    new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
                    new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0)
                };
                return _devices.D3D.Device.CreateInputLayout(inputElements, vsByteCode);
            }
        }

        private static Format DetermineFormat(Vortice.Direct3D11.Shader.RegisterComponentMaskFlags mask, Vortice.Direct3D.RegisterComponentType componentType)
        {
            var componentCount = 0;
            if (mask.HasFlag(Vortice.Direct3D11.Shader.RegisterComponentMaskFlags.ComponentX)) componentCount++;
            if (mask.HasFlag(Vortice.Direct3D11.Shader.RegisterComponentMaskFlags.ComponentY)) componentCount++;
            if (mask.HasFlag(Vortice.Direct3D11.Shader.RegisterComponentMaskFlags.ComponentZ)) componentCount++;
            if (mask.HasFlag(Vortice.Direct3D11.Shader.RegisterComponentMaskFlags.ComponentW)) componentCount++;

            if (componentType == Vortice.Direct3D.RegisterComponentType.Float32)
            {
                return componentCount switch
                {
                    1 => Format.R32_Float,
                    2 => Format.R32G32_Float,
                    3 => Format.R32G32B32_Float,
                    4 => Format.R32G32B32A32_Float,
                    _ => Format.R32G32B32A32_Float
                };
            }

            if (componentType == Vortice.Direct3D.RegisterComponentType.UInt32)
            {
                return componentCount switch
                {
                    1 => Format.R32_UInt,
                    2 => Format.R32G32_UInt,
                    3 => Format.R32G32B32_UInt,
                    4 => Format.R32G32B32A32_UInt,
                    _ => Format.R32G32B32A32_UInt
                };
            }

            if (componentType == Vortice.Direct3D.RegisterComponentType.SInt32)
            {
                return componentCount switch
                {
                    1 => Format.R32_SInt,
                    2 => Format.R32G32_SInt,
                    3 => Format.R32G32B32_SInt,
                    4 => Format.R32G32B32A32_SInt,
                    _ => Format.R32G32B32A32_SInt
                };
            }

            return Format.R32G32B32A32_Float;
        }

        private static int GetFormatSize(Format format)
        {
            return format switch
            {
                Format.R32_Float or Format.R32_UInt or Format.R32_SInt => 4,
                Format.R32G32_Float or Format.R32G32_UInt or Format.R32G32_SInt => 8,
                Format.R32G32B32_Float or Format.R32G32B32_UInt or Format.R32G32B32_SInt => 12,
                Format.R32G32B32A32_Float or Format.R32G32B32A32_UInt or Format.R32G32B32A32_SInt => 16,
                _ => 16
            };
        }

        private void CompilePixelShader(string source)
        {
            var psResult = ShaderStore.Compile(source, "PS", "ps_5_0");
            if (psResult.ByteCode == null) return;

            PixelShader = _devices.D3D.Device.CreatePixelShader(psResult.ByteCode);
        }

        private void CompileGeometryShader(string source)
        {
            var gsResult = ShaderStore.Compile(source, "GS", "gs_5_0");
            if (gsResult.ByteCode == null) return;

            GeometryShader = _devices.D3D.Device.CreateGeometryShader(gsResult.ByteCode);
        }

        private void CompileHullShader(string source)
        {
            var hsResult = ShaderStore.Compile(source, "HS", "hs_5_0");
            if (hsResult.ByteCode == null) return;

            HullShader = _devices.D3D.Device.CreateHullShader(hsResult.ByteCode);
        }

        private void CompileDomainShader(string source)
        {
            var dsResult = ShaderStore.Compile(source, "DS", "ds_5_0");
            if (dsResult.ByteCode == null) return;

            DomainShader = _devices.D3D.Device.CreateDomainShader(dsResult.ByteCode);
        }

        private void CompileComputeShader(string source)
        {
            var csResult = ShaderStore.Compile(source, "CS", "cs_5_0");
            if (csResult.ByteCode == null) return;

            ComputeShader = _devices.D3D.Device.CreateComputeShader(csResult.ByteCode);
        }

        private static string ComputeHash(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = SHA256.HashData(bytes);
            return System.Convert.ToHexString(hash);
        }

        public void Dispose()
        {
            if (VertexShader != null) { VertexShader.Dispose(); VertexShader = null; }
            if (PixelShader != null) { PixelShader.Dispose(); PixelShader = null; }
            if (GeometryShader != null) { GeometryShader.Dispose(); GeometryShader = null; }
            if (HullShader != null) { HullShader.Dispose(); HullShader = null; }
            if (DomainShader != null) { DomainShader.Dispose(); DomainShader = null; }
            if (ComputeShader != null) { ComputeShader.Dispose(); ComputeShader = null; }
            if (InputLayout != null) { InputLayout.Dispose(); InputLayout = null; }
        }
    }
}