using System.Collections.ObjectModel;
using ObjLoader.Core;
using ObjLoader.Plugin.Parameters;

namespace ObjLoader.Services.Layers
{
    public class LayerManager : ILayerManager
    {
        private int _selectedLayerIndex;
        private LayerData? _activeLayer;

        public ObservableCollection<LayerData> Layers { get; } = new ObservableCollection<LayerData>();
        public int SelectedLayerIndex => _selectedLayerIndex;
        public bool IsSwitchingLayer { get; private set; } = false;

        public void Initialize(ObjLoaderParameter parameter)
        {
            EnsureLayers(parameter);
        }

        public void EnsureLayers(ObjLoaderParameter parameter)
        {
            var validLayerIds = (parameter.LayerIds ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet();

            if (Layers.Count > 0 && validLayerIds.Count > 0 && Layers.Any(l => validLayerIds.Contains(l.Guid)))
            {
                var unauthorizedLayers = Layers.Where(l => !validLayerIds.Contains(l.Guid)).ToList();
                foreach (var layer in unauthorizedLayers)
                {
                    Layers.Remove(layer);
                }
            }

            if (Layers.Any(l => !string.IsNullOrEmpty(l.FilePath)))
            {
                var emptyDefaults = Layers
                    .Where(l => l.Name == "Default" && string.IsNullOrEmpty(l.FilePath))
                    .ToList();

                foreach (var item in emptyDefaults)
                {
                    Layers.Remove(item);
                }
            }

            if (Layers.Count == 0 && validLayerIds.Count == 0)
            {
                var defaultLayer = new LayerData { Name = "Default" };
                CopyFromParameter(defaultLayer, parameter);
                Layers.Add(defaultLayer);
            }

            if (Layers.Count > 0)
            {
                var targetLayer = Layers.FirstOrDefault(l => l.Guid == parameter.ActiveLayerGuid);

                if (targetLayer != null)
                {
                    _activeLayer = targetLayer;
                    var newIndex = Layers.IndexOf(targetLayer);

                    if (_selectedLayerIndex != newIndex)
                    {
                        _selectedLayerIndex = newIndex;
                        parameter.SelectedLayerIndex = newIndex;
                    }

                    if (!string.IsNullOrEmpty(_activeLayer.FilePath))
                    {
                        ApplyToParameter(_activeLayer, parameter);
                    }
                    else if (_activeLayer.Name == "Default")
                    {
                        CopyFromParameter(_activeLayer, parameter);
                    }
                }
                else
                {
                    var maxIndex = Layers.Count - 1;
                    var targetIndex = Math.Clamp(parameter.SelectedLayerIndex, 0, maxIndex);

                    _selectedLayerIndex = targetIndex;
                    parameter.SelectedLayerIndex = targetIndex;
                    _activeLayer = Layers[_selectedLayerIndex];

                    parameter.ActiveLayerGuid = _activeLayer.Guid;

                    if (!string.IsNullOrEmpty(_activeLayer.FilePath))
                    {
                        ApplyToParameter(_activeLayer, parameter);
                    }
                }
            }
            else
            {
                _selectedLayerIndex = -1;
                _activeLayer = null;
                parameter.ActiveLayerGuid = string.Empty;
            }
        }

        public void ChangeLayer(int newIndex, ObjLoaderParameter parameter)
        {
            if (_selectedLayerIndex == newIndex) return;

            try
            {
                SaveActiveLayer(parameter);

                IsSwitchingLayer = true;
                _selectedLayerIndex = newIndex;
                parameter.SelectedLayerIndex = newIndex;
                LoadActiveLayer(parameter);
            }
            finally
            {
                IsSwitchingLayer = false;
            }
        }

        public void SaveActiveLayer(ObjLoaderParameter parameter)
        {
            if (IsSwitchingLayer || Layers.Count == 0) return;

            LayerData? targetLayer = null;

            if (_activeLayer != null && Layers.Contains(_activeLayer))
            {
                targetLayer = _activeLayer;
            }
            else if (_selectedLayerIndex >= 0 && _selectedLayerIndex < Layers.Count)
            {
                targetLayer = Layers[_selectedLayerIndex];
            }

            if (targetLayer != null)
            {
                CopyFromParameter(targetLayer, parameter);

                var frame = (long)parameter.CurrentFrame;
                var len = (int)(parameter.Duration * parameter.CurrentFPS);
                var fps = parameter.CurrentFPS;

                var activeWorldId = (int)parameter.WorldId.GetValue(frame, len, fps);

                foreach (var l in Layers)
                {
                    if (l == targetLayer) continue;

                    var lWorldId = (int)l.WorldId.GetValue(frame, len, fps);
                    if (lWorldId == activeWorldId)
                    {
                        l.LightX.CopyFrom(parameter.LightX);
                        l.LightY.CopyFrom(parameter.LightY);
                        l.LightZ.CopyFrom(parameter.LightZ);
                        l.LightType = parameter.LightType;
                        l.IsLightEnabled = parameter.IsLightEnabled;
                    }
                }
            }
        }

        public void LoadSharedData(IEnumerable<LayerData> layers)
        {
            if (layers != null)
            {
                Layers.Clear();
                foreach (var layer in layers)
                {
                    Layers.Add(layer);
                }
            }
        }

        private void LoadActiveLayer(ObjLoaderParameter parameter)
        {
            if (Layers.Count == 0) return;
            var idx = Math.Clamp(_selectedLayerIndex, 0, Layers.Count - 1);
            var layer = Layers[idx];

            _activeLayer = layer;
            parameter.ActiveLayerGuid = layer.Guid;

            ApplyToParameter(layer, parameter);
        }

        private void CopyFromParameter(LayerData layer, ObjLoaderParameter parameter)
        {
            layer.FilePath = parameter.FilePath;
            layer.BaseColor = parameter.BaseColor;
            layer.IsLightEnabled = parameter.IsLightEnabled;
            layer.LightType = parameter.LightType;
            layer.Projection = parameter.Projection;

            layer.X.CopyFrom(parameter.X);
            layer.Y.CopyFrom(parameter.Y);
            layer.Z.CopyFrom(parameter.Z);
            layer.Scale.CopyFrom(parameter.Scale);
            layer.RotationX.CopyFrom(parameter.RotationX);
            layer.RotationY.CopyFrom(parameter.RotationY);
            layer.RotationZ.CopyFrom(parameter.RotationZ);
            layer.Fov.CopyFrom(parameter.Fov);
            layer.LightX.CopyFrom(parameter.LightX);
            layer.LightY.CopyFrom(parameter.LightY);
            layer.LightZ.CopyFrom(parameter.LightZ);
            layer.WorldId.CopyFrom(parameter.WorldId);
        }

        private void ApplyToParameter(LayerData layer, ObjLoaderParameter parameter)
        {
            parameter.FilePath = layer.FilePath;
            parameter.BaseColor = layer.BaseColor;
            parameter.IsLightEnabled = layer.IsLightEnabled;
            parameter.LightType = layer.LightType;
            parameter.Projection = layer.Projection;

            parameter.X.CopyFrom(layer.X);
            parameter.Y.CopyFrom(layer.Y);
            parameter.Z.CopyFrom(layer.Z);
            parameter.Scale.CopyFrom(layer.Scale);
            parameter.RotationX.CopyFrom(layer.RotationX);
            parameter.RotationY.CopyFrom(layer.RotationY);
            parameter.RotationZ.CopyFrom(layer.RotationZ);
            parameter.Fov.CopyFrom(layer.Fov);
            parameter.LightX.CopyFrom(layer.LightX);
            parameter.LightY.CopyFrom(layer.LightY);
            parameter.LightZ.CopyFrom(layer.LightZ);
            parameter.WorldId.CopyFrom(layer.WorldId);
        }
    }
}