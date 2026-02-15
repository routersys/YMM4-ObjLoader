using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Managers.Interfaces
{
    public interface IDynamicTextureManager : IDisposable
    {
        IReadOnlyDictionary<string, ID3D11ShaderResourceView> Textures { get; }
        void Prepare(IEnumerable<string> usedPaths, ID3D11Device device);
        void Clear();
    }
}