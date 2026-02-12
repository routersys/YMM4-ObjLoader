using System.Collections.ObjectModel;
using ObjLoader.Core;
using ObjLoader.Plugin;

namespace ObjLoader.Services.Layers
{
    public class LayerManager : ILayerManager
    {
        private const int MaxHierarchyDepth = 100;
        private const int MaxDescendantCount = 10000;

        private int _selectedLayerIndex;
        private LayerData? _activeLayer;
        private readonly object _lock = new();
        private readonly HashSet<string> _visited = new();
        private readonly Queue<string> _queue = new();

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

        public bool SetParent(string childId, string? parentId)
        {
            lock (_lock)
            {
                var child = Layers.FirstOrDefault(l => l.Guid == childId);
                if (child == null) return false;

                if (parentId == null)
                {
                    child.ParentGuid = string.Empty;
                    return true;
                }

                var parent = Layers.FirstOrDefault(l => l.Guid == parentId);
                if (parent == null) return false;

                if (childId == parentId)
                {
                    throw new InvalidOperationException($"Layer {childId} cannot be its own parent.");
                }

                if (WouldCreateCycle(childId, parentId))
                {
                    throw new InvalidOperationException(
                        $"Setting {parentId} as parent of {childId} would create a cycle.");
                }

                child.ParentGuid = parentId;
                return true;
            }
        }

        public bool GetEffectiveVisibility(string layerId)
        {
            lock (_lock)
            {
                _visited.Clear();
                var current = layerId;
                int depth = 0;

                while (!string.IsNullOrEmpty(current))
                {
                    if (!_visited.Add(current))
                    {
                        throw new InvalidOperationException($"Cycle detected for layer: {layerId}");
                    }

                    if (depth > MaxHierarchyDepth)
                    {
                        throw new InvalidOperationException($"Hierarchy depth exceeds limit ({MaxHierarchyDepth}) for layer: {layerId}");
                    }

                    var layer = Layers.FirstOrDefault(l => l.Guid == current);
                    if (layer == null) break;

                    if (!layer.IsVisible) return false;

                    current = layer.ParentGuid;
                    depth++;
                }

                return true;
            }
        }

        public List<string> GetAllDescendants(string layerId)
        {
            lock (_lock)
            {
                var result = new List<string>();
                _queue.Clear();
                _visited.Clear();

                if (!Layers.Any(l => l.Guid == layerId))
                {
                    return result;
                }

                _queue.Enqueue(layerId);
                _visited.Add(layerId);

                while (_queue.Count > 0)
                {
                    var current = _queue.Dequeue();

                    foreach (var child in Layers.Where(l => l.ParentGuid == current))
                    {
                        if (_visited.Add(child.Guid))
                        {
                            result.Add(child.Guid);
                            _queue.Enqueue(child.Guid);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Cycle detected at: {child.Guid}");
                        }
                    }

                    if (result.Count > MaxDescendantCount)
                    {
                        throw new InvalidOperationException($"Descendant count exceeds limit ({MaxDescendantCount}).");
                    }
                }

                return result;
            }
        }

        public ValidationResult ValidateHierarchy()
        {
            lock (_lock)
            {
                var result = new ValidationResult();

                foreach (var layer in Layers)
                {
                    _visited.Clear();
                    int depth = 0;

                    var current = layer.Guid;
                    while (!string.IsNullOrEmpty(current))
                    {
                        if (!_visited.Add(current))
                        {
                            result.Errors.Add($"Cycle detected: {string.Join(" → ", _visited)} → {current}");
                            break;
                        }

                        if (depth > MaxHierarchyDepth)
                        {
                            result.Errors.Add($"Hierarchy depth exceeds limit ({MaxHierarchyDepth}) at layer: {current}");
                            break;
                        }

                        var found = Layers.FirstOrDefault(l => l.Guid == current);
                        if (found == null) break;

                        current = found.ParentGuid;
                        depth++;
                    }

                    if (!string.IsNullOrEmpty(layer.ParentGuid) && !Layers.Any(l => l.Guid == layer.ParentGuid))
                    {
                        result.Warnings.Add($"Layer {layer.Guid} references non-existent parent {layer.ParentGuid}.");
                    }
                }

                return result;
            }
        }

        private bool WouldCreateCycle(string childId, string potentialParentId)
        {
            _visited.Clear();
            var current = potentialParentId;
            int depth = 0;

            while (!string.IsNullOrEmpty(current))
            {
                if (!_visited.Add(current)) return true;
                if (current == childId) return true;

                var layer = Layers.FirstOrDefault(l => l.Guid == current);
                if (layer == null) break;

                current = layer.ParentGuid;
                depth++;
                if (depth > MaxHierarchyDepth)
                {
                    throw new InvalidOperationException($"Hierarchy depth exceeds limit ({MaxHierarchyDepth}) or existing cycle detected.");
                }
            }

            return false;
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