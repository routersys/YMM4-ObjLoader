using ObjLoader.Api.Attachment;
using ObjLoader.Api.Camera;
using ObjLoader.Api.DepthBuffer;
using ObjLoader.Api.Draw;
using ObjLoader.Api.Events;
using ObjLoader.Api.Light;
using ObjLoader.Api.Material;
using ObjLoader.Api.Objects;
using ObjLoader.Api.Raycast;

namespace ObjLoader.Api.Core
{
    public interface ISceneServices
    {
        ICameraApi Camera { get; }
        ISceneDrawApi Draw { get; }
        IObjectApi Objects { get; }
        ILightApi Lights { get; }
        IMaterialApi Materials { get; }
        IDepthBufferApi DepthBuffer { get; }
        IRaycastApi Raycast { get; }
        ISceneEventApi Events { get; }
        IAttachmentApi Attachments { get; }
        bool IsDisposed { get; }
        nint ContextPointer { get; }
        void TriggerUpdate();
    }
}