using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Managers.Interfaces
{
    internal interface IEnvironmentMapManager : IDisposable
    {
        ID3D11Texture2D EnvironmentCubeMap { get; }
        ID3D11ShaderResourceView EnvironmentSRV { get; }
        ID3D11RenderTargetView[] EnvironmentRTVs { get; }
        ID3D11DepthStencilView EnvironmentDSV { get; }
    }
}