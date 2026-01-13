using System.Collections.ObjectModel;
using ObjLoader.Core;
using ObjLoader.Plugin;

namespace ObjLoader.Services
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
            if (Layers.Count > 0)
            {
                var idx = Math.Clamp(_selectedLayerIndex, 0, Layers.Count - 1);
                _activeLayer = Layers[idx];
            }
        }

        public void EnsureLayers(ObjLoaderParameter parameter)
        {
            if (Layers.Count == 0)
            {
                var defaultLayer = new LayerData { Name = "Default" };
                CopyFromParameter(defaultLayer, parameter);
                Layers.Add(defaultLayer);
                _selectedLayerIndex = 0;
                _activeLayer = defaultLayer;
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
            else if (_activeLayer == null && _selectedLayerIndex >= 0 && _selectedLayerIndex < Layers.Count)
            {
                targetLayer = Layers[_selectedLayerIndex];
            }

            if (targetLayer != null)
            {
                CopyFromParameter(targetLayer, parameter);
            }
        }

        public void LoadSharedData(IEnumerable<LayerData> layers)
        {
            Layers.Clear();
            foreach (var layer in layers)
            {
                Layers.Add(layer);
            }
        }

        private void LoadActiveLayer(ObjLoaderParameter parameter)
        {
            if (Layers.Count == 0) return;
            var idx = Math.Clamp(_selectedLayerIndex, 0, Layers.Count - 1);
            var layer = Layers[idx];
            _activeLayer = layer;
            ApplyToParameter(layer, parameter);
        }

        private void CopyFromParameter(LayerData layer, ObjLoaderParameter parameter)
        {
            layer.FilePath = parameter.FilePath;
            layer.BaseColor = parameter.BaseColor;
            layer.IsLightEnabled = parameter.IsLightEnabled;
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