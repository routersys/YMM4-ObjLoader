using ObjLoader.Api.Attachment;
using ObjLoader.Api.Camera;
using ObjLoader.Api.Core;
using ObjLoader.Api.DepthBuffer;
using ObjLoader.Api.Draw;
using ObjLoader.Api.Events;
using ObjLoader.Api.Light;
using ObjLoader.Api.Material;
using ObjLoader.Api.Objects;
using ObjLoader.Api.Raycast;
using ObjLoader.Api.Transaction;
using ObjLoader.Plugin;
using ObjLoader.Services.Layers;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct3D11;

namespace ObjLoader.Api
{
    internal sealed class ObjLoaderSceneApi : ISceneServices, IDisposable
    {
        private readonly CameraApi _camera;
        private readonly SceneDrawApi _draw;
        private readonly ObjectApi _objects;
        private readonly LightApi _lights;
        private readonly MaterialApi _materials;
        private readonly DepthBufferApi _depthBuffer;
        private readonly RaycastApi _raycast;
        private readonly SceneEventApi _events;
        private readonly TransactionApi _transactions;
        private readonly AttachmentApi _attachments;
        private readonly Func<ID2D1Image?> _forceRenderProvider;
        private readonly ObjLoaderParameter _parameter;
        private bool _isDisposed;

        private readonly nint _contextPointer;

        public ICameraApi Camera => _camera;
        public ISceneDrawApi Draw => _draw;
        public IObjectApi Objects => _objects;
        public ILightApi Lights => _lights;
        public IMaterialApi Materials => _materials;
        public IDepthBufferApi DepthBuffer => _depthBuffer;
        public IRaycastApi Raycast => _raycast;
        public ISceneEventApi Events => _events;
        public ITransactionApi Transactions => _transactions;
        public IAttachmentApi Attachments => _attachments;
        public nint ContextPointer => _contextPointer;

        internal ObjLoaderSceneApi(
            ObjLoaderParameter parameter,
            ILayerManager layerManager,
            Func<(ID3D11Texture2D? Texture, int Width, int Height)> depthTextureProvider,
            Func<ID3D11ShaderResourceView?> depthSrvProvider,
            Func<(Matrix4x4 View, Matrix4x4 Proj, int Width, int Height)> cameraMatrixProvider,
            Func<ID2D1Image?> forceRenderProvider,
            nint contextPointer)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));
            if (layerManager == null) throw new ArgumentNullException(nameof(layerManager));
            if (depthTextureProvider == null) throw new ArgumentNullException(nameof(depthTextureProvider));
            if (depthSrvProvider == null) throw new ArgumentNullException(nameof(depthSrvProvider));
            if (cameraMatrixProvider == null) throw new ArgumentNullException(nameof(cameraMatrixProvider));

            _parameter = parameter;

            var eventApi = new SceneEventApi();

            _camera = new CameraApi(parameter);
            _draw = new SceneDrawApi();
            _objects = new ObjectApi(layerManager);
            _lights = new LightApi(layerManager, parameter);
            _materials = new MaterialApi(layerManager);
            _depthBuffer = new DepthBufferApi(depthTextureProvider, depthSrvProvider);
            _raycast = new RaycastApi(layerManager, cameraMatrixProvider);
            _events = eventApi;
            _transactions = new TransactionApi(eventApi);
            _attachments = new AttachmentApi(layerManager);
            _forceRenderProvider = forceRenderProvider ?? throw new ArgumentNullException(nameof(forceRenderProvider));
            _contextPointer = contextPointer;
        }

        internal SceneDrawApi DrawInternal => _draw;
        public ID2D1Image? ForceRender() => _forceRenderProvider();

        public void TriggerUpdate()
        {
            if (!_isDisposed)
            {
                _parameter.ForceUpdate();
            }
        }

        public bool IsDisposed => _isDisposed;

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
        }
    }
}