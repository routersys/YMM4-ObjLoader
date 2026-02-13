using ObjLoader.Rendering.Core;
using ObjLoader.Rendering.Managers.Interfaces;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ObjLoader.Rendering.Managers
{
    internal sealed class EnvironmentMapManager : IEnvironmentMapManager
    {
        private ID3D11Texture2D? _environmentCubeMap;
        private ID3D11ShaderResourceView? _environmentSRV;
        private ID3D11RenderTargetView[]? _environmentRTVs;
        private ID3D11DepthStencilView? _environmentDSV;
        private ID3D11Texture2D? _environmentDepthTexture;

        private readonly object _lock = new object();
        private bool _disposed;

        public bool IsInitialized => _environmentCubeMap != null;
        public ID3D11Texture2D? EnvironmentCubeMap => _environmentCubeMap;
        public ID3D11ShaderResourceView? EnvironmentSRV => _environmentSRV;
        public ID3D11RenderTargetView[]? EnvironmentRTVs => _environmentRTVs;
        public ID3D11DepthStencilView? EnvironmentDSV => _environmentDSV;

        public void Initialize(ID3D11Device device)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));

            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(EnvironmentMapManager));
                if (_environmentCubeMap != null) return;

                _environmentCubeMap = CreateEnvironmentCubeMap(device);
                _environmentSRV = CreateEnvironmentSRV(device, _environmentCubeMap);
                _environmentRTVs = CreateEnvironmentRTVs(device, _environmentCubeMap);
                (_environmentDepthTexture, _environmentDSV) = CreateEnvironmentDepthResources(device);
            }
        }

        private static ID3D11Texture2D CreateEnvironmentCubeMap(ID3D11Device device)
        {
            var envDesc = new Texture2DDescription
            {
                Width = RenderingConstants.EnvironmentMapSize,
                Height = RenderingConstants.EnvironmentMapSize,
                MipLevels = 0,
                ArraySize = RenderingConstants.EnvironmentMapFaceCount,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                MiscFlags = ResourceOptionFlags.TextureCube | ResourceOptionFlags.GenerateMips
            };
            return device.CreateTexture2D(envDesc);
        }

        private static ID3D11ShaderResourceView CreateEnvironmentSRV(ID3D11Device device, ID3D11Texture2D cubeMap)
        {
            var envSrvDesc = new ShaderResourceViewDescription
            {
                Format = Format.R8G8B8A8_UNorm,
                ViewDimension = ShaderResourceViewDimension.TextureCube,
                TextureCube = new TextureCubeShaderResourceView
                {
                    MipLevels = -1,
                    MostDetailedMip = 0
                }
            };
            return device.CreateShaderResourceView(cubeMap, envSrvDesc);
        }

        private static ID3D11RenderTargetView[] CreateEnvironmentRTVs(ID3D11Device device, ID3D11Texture2D cubeMap)
        {
            var rtvs = new ID3D11RenderTargetView[RenderingConstants.EnvironmentMapFaceCount];

            for (int i = 0; i < RenderingConstants.EnvironmentMapFaceCount; i++)
            {
                var rtvDesc = new RenderTargetViewDescription
                {
                    Format = Format.R8G8B8A8_UNorm,
                    ViewDimension = RenderTargetViewDimension.Texture2DArray,
                    Texture2DArray = new Texture2DArrayRenderTargetView
                    {
                        ArraySize = 1,
                        FirstArraySlice = i,
                        MipSlice = 0
                    }
                };
                rtvs[i] = device.CreateRenderTargetView(cubeMap, rtvDesc);
            }

            return rtvs;
        }

        private static (ID3D11Texture2D, ID3D11DepthStencilView) CreateEnvironmentDepthResources(ID3D11Device device)
        {
            var envDepthDesc = new Texture2DDescription
            {
                Width = RenderingConstants.EnvironmentMapSize,
                Height = RenderingConstants.EnvironmentMapSize,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil
            };
            var depthTex = device.CreateTexture2D(envDepthDesc);

            var envDsvDesc = new DepthStencilViewDescription
            {
                Format = Format.D24_UNorm_S8_UInt,
                ViewDimension = DepthStencilViewDimension.Texture2D
            };
            var dsv = device.CreateDepthStencilView(depthTex, envDsvDesc);

            return (depthTex, dsv);
        }

        private void DisposeResources()
        {
            SafeDispose(ref _environmentDSV);
            SafeDispose(ref _environmentDepthTexture);

            if (_environmentRTVs != null)
            {
                foreach (var rtv in _environmentRTVs)
                {
                    SafeDisposeValue(rtv);
                }
                _environmentRTVs = null;
            }

            SafeDispose(ref _environmentSRV);
            SafeDispose(ref _environmentCubeMap);
        }

        private static void SafeDispose<T>(ref T? disposable) where T : class, IDisposable
        {
            var temp = disposable;
            disposable = null;
            SafeDisposeValue(temp);
        }

        private static void SafeDisposeValue(IDisposable? disposable)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                DisposeResources();
            }
        }
    }
}