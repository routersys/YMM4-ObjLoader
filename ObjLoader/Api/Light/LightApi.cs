using ObjLoader.Api.Core;
using ObjLoader.Plugin;
using ObjLoader.Services.Layers;
using System.Numerics;

namespace ObjLoader.Api.Light
{
    internal sealed class LightApi : ILightApi
    {
        private readonly ILayerManager _layerManager;
        private readonly ObjLoaderParameter _parameter;

        internal LightApi(ILayerManager layerManager, ObjLoaderParameter parameter)
        {
            _layerManager = layerManager ?? throw new ArgumentNullException(nameof(layerManager));
            _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

        public ApiResult<LightDescriptor> GetLight(int worldId)
        {
            foreach (var layer in _layerManager.Layers)
            {
                if ((int)layer.WorldId.GetValue(0, 1, 1) == worldId && layer.IsLightEnabled)
                {
                    var desc = new LightDescriptor(
                        layer.IsLightEnabled,
                        layer.LightType,
                        new Vector3((float)layer.LightX.GetValue(0, 1, 1), (float)layer.LightY.GetValue(0, 1, 1), (float)layer.LightZ.GetValue(0, 1, 1)),
                        layer.BaseColor,
                        1.0f);
                    return ApiResult<LightDescriptor>.Ok(desc);
                }
            }
            return ApiResult<LightDescriptor>.Fail($"No light found for worldId: {worldId}");
        }

        public bool SetLight(int worldId, in LightDescriptor light)
        {
            bool found = false;
            foreach (var layer in _layerManager.Layers)
            {
                if ((int)layer.WorldId.GetValue(0, 1, 1) == worldId)
                {
                    layer.IsLightEnabled = light.IsEnabled;
                    layer.LightType = light.Type;
                    layer.LightX.CopyFrom(new YukkuriMovieMaker.Commons.Animation(light.Position.X, -100000, 100000));
                    layer.LightY.CopyFrom(new YukkuriMovieMaker.Commons.Animation(light.Position.Y, -100000, 100000));
                    layer.LightZ.CopyFrom(new YukkuriMovieMaker.Commons.Animation(light.Position.Z, -100000, 100000));
                    found = true;
                }
            }
            return found;
        }

        public IReadOnlyList<int> GetWorldIds()
        {
            var ids = new HashSet<int>();
            foreach (var layer in _layerManager.Layers)
                ids.Add((int)layer.WorldId.GetValue(0, 1, 1));
            return new List<int>(ids);
        }
    }
}