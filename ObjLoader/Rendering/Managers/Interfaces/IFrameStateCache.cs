using ObjLoader.Rendering.Models;

namespace ObjLoader.Rendering.Managers.Interfaces
{
    internal interface IFrameStateCache : IDisposable
    {
        FrameState GetOrCreateState();
        void SaveState(long frame, FrameState state);
        bool TryGetState(long frame, out FrameState state);
        void Clear();
    }
}