using ObjLoader.Api.Draw;
using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Managers.Interfaces
{
    internal interface ISceneDrawManager : IDisposable
    {
        bool IsDirty { get; }
        void UpdateFromApi(SceneDrawApi api);
        IReadOnlyCollection<ExternalObjectHandle> GetExternalObjects();
        IReadOnlyCollection<(Api.Core.SceneObjectId Id, BillboardDescriptor Desc)> GetBillboards();
        ID3D11ShaderResourceView? GetBillboardSrv(Api.Core.SceneObjectId id);
        void ClearDirtyFlag();
        void Clear();
    }
}