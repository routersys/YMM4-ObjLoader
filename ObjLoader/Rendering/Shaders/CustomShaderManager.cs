using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using ObjLoader.Plugin;

namespace ObjLoader.Rendering.Shaders
{
    internal class CustomShaderManager : IDisposable
    {
        private readonly IGraphicsDevicesAndContext _devices;
        private string _loadedShaderPath = string.Empty;

        public ID3D11VertexShader? VertexShader { get; private set; }
        public ID3D11PixelShader? PixelShader { get; private set; }
        public ID3D11InputLayout? InputLayout { get; private set; }

        public CustomShaderManager(IGraphicsDevicesAndContext devices)
        {
            _devices = devices;
        }

        public void Update(string path, ObjLoaderParameter parameter)
        {
            if (path == _loadedShaderPath && VertexShader != null) return;

            Dispose();

            _loadedShaderPath = path;
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var source = parameter.GetAdaptedShaderSource();
                if (string.IsNullOrEmpty(source)) return;

                var vsResult = ShaderStore.Compile(source, "VS", "vs_5_0");
                if (vsResult.Blob != null)
                {
                    using (vsResult.Blob)
                    {
                        VertexShader = _devices.D3D.Device.CreateVertexShader(vsResult.Blob.AsBytes());
                        var inputElements = new[] {
                            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
                            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0)
                        };
                        InputLayout = _devices.D3D.Device.CreateInputLayout(inputElements, vsResult.Blob.AsBytes());
                    }
                }

                var psResult = ShaderStore.Compile(source, "PS", "ps_5_0");
                if (psResult.Blob != null)
                {
                    using (psResult.Blob)
                    {
                        PixelShader = _devices.D3D.Device.CreatePixelShader(psResult.Blob.AsBytes());
                    }
                }
            }
            catch { }
        }

        public void Dispose()
        {
            if (VertexShader != null) { VertexShader.Dispose(); VertexShader = null; }
            if (PixelShader != null) { PixelShader.Dispose(); PixelShader = null; }
            if (InputLayout != null) { InputLayout.Dispose(); InputLayout = null; }
        }
    }
}