using ObjLoader.Api.Core;
using ObjLoader.Core.Timeline;
using ObjLoader.Services.Layers;
using System.Numerics;

namespace ObjLoader.Api.Objects
{
    internal sealed class ObjectApi : IObjectApi
    {
        private readonly ILayerManager _layerManager;

        internal ObjectApi(ILayerManager layerManager)
        {
            _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
        }

        public IReadOnlyList<SceneObjectId> GetAllObjectIds()
        {
            var result = new List<SceneObjectId>();
            foreach (var layer in _layerManager.Layers)
                result.Add(new SceneObjectId(layer.Guid));
            return result;
        }

        public ApiResult<ObjectInfo> GetObjectInfo(SceneObjectId id)
        {
            var layer = FindLayer(id);
            if (layer == null) return ApiResult<ObjectInfo>.Fail($"Object not found: {id}");

            var t = new Transform(
                new Vector3((float)layer.X.GetValue(0, 1, 1), (float)layer.Y.GetValue(0, 1, 1), (float)layer.Z.GetValue(0, 1, 1)),
                new Vector3((float)layer.RotationX.GetValue(0, 1, 1), (float)layer.RotationY.GetValue(0, 1, 1), (float)layer.RotationZ.GetValue(0, 1, 1)),
                Vector3.One * (float)(layer.Scale.GetValue(0, 1, 1) / 100.0));
            return ApiResult<ObjectInfo>.Ok(new ObjectInfo(id, layer.Name ?? string.Empty, layer.FilePath ?? string.Empty, t, layer.IsVisible, (int)layer.WorldId.GetValue(0, 1, 1)));
        }

        public ApiResult<Transform> GetTransform(SceneObjectId id)
        {
            var layer = FindLayer(id);
            if (layer == null) return ApiResult<Transform>.Fail($"Object not found: {id}");

            var t = new Transform(
                new Vector3((float)layer.X.GetValue(0, 1, 1), (float)layer.Y.GetValue(0, 1, 1), (float)layer.Z.GetValue(0, 1, 1)),
                new Vector3((float)layer.RotationX.GetValue(0, 1, 1), (float)layer.RotationY.GetValue(0, 1, 1), (float)layer.RotationZ.GetValue(0, 1, 1)),
                Vector3.One * (float)(layer.Scale.GetValue(0, 1, 1) / 100.0));
            return ApiResult<Transform>.Ok(t);
        }

        public bool SetTransform(SceneObjectId id, in Transform transform)
        {
            var layer = FindLayer(id);
            if (layer == null) return false;

            layer.X.CopyFrom(new YukkuriMovieMaker.Commons.Animation(transform.Position.X, -100000, 100000));
            layer.Y.CopyFrom(new YukkuriMovieMaker.Commons.Animation(transform.Position.Y, -100000, 100000));
            layer.Z.CopyFrom(new YukkuriMovieMaker.Commons.Animation(transform.Position.Z, -100000, 100000));
            layer.RotationX.CopyFrom(new YukkuriMovieMaker.Commons.Animation(transform.RotationEulerDegrees.X, -36000, 36000));
            layer.RotationY.CopyFrom(new YukkuriMovieMaker.Commons.Animation(transform.RotationEulerDegrees.Y, -36000, 36000));
            layer.RotationZ.CopyFrom(new YukkuriMovieMaker.Commons.Animation(transform.RotationEulerDegrees.Z, -36000, 36000));
            layer.Scale.CopyFrom(new YukkuriMovieMaker.Commons.Animation(transform.Scale.X * 100.0, 0, 100000));
            return true;
        }

        public bool SetVisibility(SceneObjectId id, bool visible)
        {
            var layer = FindLayer(id);
            if (layer == null) return false;
            layer.IsVisible = visible;
            return true;
        }

        public bool SetParent(SceneObjectId childId, SceneObjectId? parentId)
        {
            try
            {
                return _layerManager.SetParent(childId.Guid, parentId?.Guid);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public IReadOnlyList<SceneObjectId> GetChildren(SceneObjectId id)
        {
            var result = new List<SceneObjectId>();
            foreach (var guid in _layerManager.GetAllDescendants(id.Guid))
                result.Add(new SceneObjectId(guid));
            return result;
        }

        private LayerData? FindLayer(SceneObjectId id)
        {
            foreach (var layer in _layerManager.Layers)
            {
                if (layer.Guid == id.Guid) return layer;
            }
            return null;
        }
    }
}