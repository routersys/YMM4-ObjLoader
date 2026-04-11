using ObjLoader.Api.Core;
using System.Collections.Concurrent;

namespace ObjLoader.Api.Draw
{
    internal sealed class SceneDrawApi : ISceneDrawApi
    {
        private readonly ConcurrentDictionary<SceneObjectId, ExternalObjectHandle> _externalObjects = new();
        private readonly ConcurrentDictionary<SceneObjectId, BillboardDescriptor> _billboards = new();

        private readonly List<ExternalObjectHandle> _visibleExternalObjectsBuf = new();
        private readonly List<(SceneObjectId Id, BillboardDescriptor Desc)> _billboardsBuf = new();

        public SceneObjectId AddExternalObject(ExternalObjectDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));

            var id = SceneObjectId.NewId();
            var handle = new ExternalObjectHandle(id, descriptor);
            _externalObjects[id] = handle;
            return id;
        }

        public bool UpdateExternalObject(SceneObjectId id, in Transform transform)
        {
            if (!_externalObjects.TryGetValue(id, out var handle)) return false;
            handle.CurrentTransform = transform;
            return true;
        }

        public bool SetExternalObjectVisibility(SceneObjectId id, bool visible)
        {
            if (!_externalObjects.TryGetValue(id, out var handle)) return false;
            handle.IsVisible = visible;
            return true;
        }

        public bool RemoveExternalObject(SceneObjectId id)
        {
            return _externalObjects.TryRemove(id, out _);
        }

        public ApiResult<ExternalObjectHandle> GetExternalObject(SceneObjectId id)
        {
            if (_externalObjects.TryGetValue(id, out var handle))
                return ApiResult<ExternalObjectHandle>.Ok(handle);
            return ApiResult<ExternalObjectHandle>.Fail($"External object not found: {id}");
        }

        public IReadOnlyList<SceneObjectId> GetExternalObjectIds()
        {
            return new List<SceneObjectId>(_externalObjects.Keys);
        }

        public SceneObjectId CreateDynamicBillboard(BillboardDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            var id = SceneObjectId.NewId();
            _billboards[id] = descriptor;
            return id;
        }

        public bool UpdateBillboard(SceneObjectId id, BillboardDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (!_billboards.ContainsKey(id)) return false;
            _billboards[id] = descriptor;
            return true;
        }

        public bool RemoveBillboard(SceneObjectId id)
        {
            return _billboards.TryRemove(id, out _);
        }

        public IReadOnlyList<SceneObjectId> GetBillboardIds()
        {
            return new List<SceneObjectId>(_billboards.Keys);
        }

        internal IReadOnlyList<ExternalObjectHandle> GetVisibleExternalObjects()
        {
            _visibleExternalObjectsBuf.Clear();
            foreach (var h in _externalObjects.Values)
            {
                if (h.IsVisible) _visibleExternalObjectsBuf.Add(h);
            }
            return _visibleExternalObjectsBuf;
        }

        internal IReadOnlyList<(SceneObjectId Id, BillboardDescriptor Desc)> GetBillboards()
        {
            _billboardsBuf.Clear();
            foreach (var kv in _billboards)
                _billboardsBuf.Add((kv.Key, kv.Value));
            return _billboardsBuf;
        }
    }
}