using ObjLoader.Api.Core;
using ObjLoader.Services.Layers;
using System.Collections.Concurrent;

namespace ObjLoader.Api.Attachment
{
    internal sealed class AttachmentApi : IAttachmentApi
    {
        private readonly ILayerManager _layerManager;
        private readonly ConcurrentDictionary<string, AttachmentHandle> _handles = new();

        internal AttachmentApi(ILayerManager layerManager)
        {
            _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        }

        public AttachmentHandle AttachObject(SceneObjectId childId, SceneObjectId parentId, in Transform localOffset)
        {
            bool success = _layerManager.SetParent(childId.Guid, parentId.Guid);
            if (!success) throw new InvalidOperationException($"Cannot attach {childId} to {parentId}.");

            var handle = new AttachmentHandle(childId, parentId, localOffset);
            _handles[childId.Guid] = handle;
            return handle;
        }

        public bool UpdateAttachmentOffset(AttachmentHandle handle, in Transform localOffset)
        {
            if (handle == null) return false;
            if (!_handles.ContainsKey(handle.ChildId.Guid)) return false;
            handle.LocalOffset = localOffset;
            return true;
        }

        public bool Detach(AttachmentHandle handle)
        {
            if (handle == null) return false;

            bool success = _layerManager.SetParent(handle.ChildId.Guid, null);
            _handles.TryRemove(handle.ChildId.Guid, out _);
            return success;
        }

        public IReadOnlyList<AttachmentHandle> GetAttachments(SceneObjectId objectId)
        {
            var result = new List<AttachmentHandle>();
            foreach (var h in _handles.Values)
            {
                if (h.ParentId == objectId || h.ChildId == objectId)
                    result.Add(h);
            }
            return result;
        }
    }
}