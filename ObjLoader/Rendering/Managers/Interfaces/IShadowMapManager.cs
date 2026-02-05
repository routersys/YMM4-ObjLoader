using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Managers.Interfaces
{
    internal interface IShadowMapManager : IDisposable
    {
        ID3D11Texture2D? ShadowMapTexture { get; }
        ID3D11DepthStencilView[]? ShadowMapDSVs { get; }
        ID3D11ShaderResourceView? ShadowMapSRV { get; }
        int CurrentShadowMapSize { get; }
        bool IsCascaded { get; }
        void EnsureShadowMapSize(ID3D11Device device, int size, bool useCascaded);
    }
}