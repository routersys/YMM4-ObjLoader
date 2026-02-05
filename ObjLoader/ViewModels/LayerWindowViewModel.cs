using ObjLoader.Core;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Utilities;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    internal class LayerWindowViewModel : Bindable, IDisposable
    {
        private readonly ObjLoaderParameter _parameter;
        private readonly ObjModelLoader _loader = new ObjModelLoader();
        private bool _suppressCollectionChanged = false;

        public ObservableCollection<LayerItemViewModel> Layers { get; } = new ObservableCollection<LayerItemViewModel>();

        private LayerItemViewModel? _selectedLayer;
        public LayerItemViewModel? SelectedLayer
        {
            get => _selectedLayer;
            set
            {
                if (_selectedLayer != value)
                {
                    _selectedLayer = value;
                    OnPropertyChanged();

                    if (_selectedLayer != null)
                    {
                        _parameter.SelectedLayerIndex = Layers.IndexOf(_selectedLayer);
                        _parameter.ActiveLayerGuid = _selectedLayer.Data.Guid;
                    }
                    else
                    {
                        _parameter.SelectedLayerIndex = -1;
                        _parameter.ActiveLayerGuid = string.Empty;
                    }

                    UpdateCommands();
                }
            }
        }

        public ActionCommand AddLayerCommand { get; }
        public ActionCommand RemoveLayerCommand { get; }
        public ActionCommand MoveUpLayerCommand { get; }
        public ActionCommand MoveDownLayerCommand { get; }
        public ActionCommand RenameLayerCommand { get; }

        public LayerWindowViewModel(ObjLoaderParameter parameter)
        {
            _parameter = parameter;

            SyncLayers();

            LayerItemViewModel? targetLayer = null;
            if (!string.IsNullOrEmpty(_parameter.ActiveLayerGuid))
            {
                targetLayer = Layers.FirstOrDefault(l => l.Data.Guid == _parameter.ActiveLayerGuid);
            }

            if (targetLayer == null && _parameter.SelectedLayerIndex >= 0 && _parameter.SelectedLayerIndex < Layers.Count)
            {
                targetLayer = Layers[_parameter.SelectedLayerIndex];
            }
            else if (targetLayer == null && Layers.Count > 0)
            {
                targetLayer = Layers[0];
            }

            _selectedLayer = targetLayer;
            OnPropertyChanged(nameof(SelectedLayer));

            if (_selectedLayer != null)
            {
                if (_parameter.SelectedLayerIndex != Layers.IndexOf(_selectedLayer))
                {
                    _parameter.SelectedLayerIndex = Layers.IndexOf(_selectedLayer);
                }
                if (_parameter.ActiveLayerGuid != _selectedLayer.Data.Guid)
                {
                    _parameter.ActiveLayerGuid = _selectedLayer.Data.Guid;
                }
            }

            PropertyChangedEventManager.AddHandler(_parameter, OnParameterPropertyChanged, string.Empty);
            CollectionChangedEventManager.AddHandler(_parameter.Layers, OnLayersCollectionChanged);

            AddLayerCommand = new ActionCommand(_ => true, _ => AddLayer());
            RemoveLayerCommand = new ActionCommand(_ => CanRemove(), _ => RemoveLayer());
            MoveUpLayerCommand = new ActionCommand(_ => CanMoveUp(), _ => MoveUp());
            MoveDownLayerCommand = new ActionCommand(_ => CanMoveDown(), _ => MoveDown());
            RenameLayerCommand = new ActionCommand(_ => SelectedLayer != null, _ => RenameLayer());
        }

        private void SyncLayers()
        {
            var dict = new Dictionary<string, LayerData>();
            foreach (var layer in _parameter.Layers)
            {
                if (!dict.ContainsKey(layer.Guid))
                {
                    dict[layer.Guid] = layer;
                }
            }

            var targetList = new List<(LayerData Data, int Depth, int SeqIdx)>();
            int childCounter = 1;

            foreach (var layer in _parameter.Layers)
            {
                int depth = 0;
                var current = layer;
                while (!string.IsNullOrEmpty(current.ParentGuid) && dict.TryGetValue(current.ParentGuid, out var parent))
                {
                    if (current.Guid == parent.Guid) break;
                    depth++;
                    current = parent;
                    if (depth > 20) break;
                }

                if (depth == 0)
                {
                    childCounter = 1;
                }

                int seqIdx = (depth > 0) ? childCounter++ : 0;
                targetList.Add((layer, depth, seqIdx));
            }

            var targetGuids = new HashSet<string>(targetList.Select(x => x.Data.Guid));
            for (int i = Layers.Count - 1; i >= 0; i--)
            {
                if (!targetGuids.Contains(Layers[i].Data.Guid))
                {
                    Layers[i].Dispose();
                    Layers.RemoveAt(i);
                }
            }

            for (int i = 0; i < targetList.Count; i++)
            {
                var target = targetList[i];
                LayerItemViewModel? existingVM = null;
                int existingIndex = -1;

                for (int j = i; j < Layers.Count; j++)
                {
                    if (Layers[j].Data.Guid == target.Data.Guid)
                    {
                        existingVM = Layers[j];
                        existingIndex = j;
                        break;
                    }
                }

                if (existingVM == null)
                {
                    var vm = Layers.FirstOrDefault(l => l.Data.Guid == target.Data.Guid);
                    if (vm != null) existingIndex = Layers.IndexOf(vm);
                    existingVM = vm;
                }

                if (existingVM != null)
                {
                    existingVM.Depth = target.Depth;
                    existingVM.SequentialIndex = target.SeqIdx;

                    if (existingIndex != i)
                    {
                        Layers.Move(existingIndex, i);
                    }
                }
                else
                {
                    var newVM = new LayerItemViewModel(target.Data, _loader, () => _parameter.ForceUpdate())
                    {
                        Depth = target.Depth,
                        SequentialIndex = target.SeqIdx
                    };
                    Layers.Insert(i, newVM);
                }
            }
        }

        private void OnLayersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppressCollectionChanged) return;

            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var currentGuid = _parameter.ActiveLayerGuid;

                    SyncLayers();

                    LayerItemViewModel? nextSelection = null;
                    if (!string.IsNullOrEmpty(currentGuid))
                    {
                        nextSelection = Layers.FirstOrDefault(l => l.Data.Guid == currentGuid);
                    }

                    if (nextSelection == null && _parameter.SelectedLayerIndex >= 0 && _parameter.SelectedLayerIndex < Layers.Count)
                    {
                        nextSelection = Layers[_parameter.SelectedLayerIndex];
                    }

                    SelectedLayer = nextSelection;
                    UpdateCommands();
                });
            }
        }

        private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ObjLoaderParameter.SelectedLayerIndex))
            {
                var idx = _parameter.SelectedLayerIndex;
                if (idx >= 0 && idx < Layers.Count && (SelectedLayer == null || Layers.IndexOf(SelectedLayer) != idx))
                {
                    _selectedLayer = Layers[idx];
                    OnPropertyChanged(nameof(SelectedLayer));

                    if (_selectedLayer != null)
                    {
                        _parameter.ActiveLayerGuid = _selectedLayer.Data.Guid;
                    }
                    else
                    {
                        _parameter.ActiveLayerGuid = string.Empty;
                    }

                    UpdateCommands();
                }
            }
            else if (e.PropertyName == nameof(ObjLoaderParameter.FilePath))
            {
                if (SelectedLayer != null &&
                    Layers.IndexOf(SelectedLayer) == _parameter.SelectedLayerIndex &&
                    SelectedLayer.Data.FilePath != _parameter.FilePath)
                {
                    SelectedLayer.Data.FilePath = _parameter.FilePath;
                    SelectedLayer.UpdateThumbnail();
                    SelectedLayer.UpdateName();
                }
            }
        }

        private void UpdateCommands()
        {
            RemoveLayerCommand.RaiseCanExecuteChanged();
            MoveUpLayerCommand.RaiseCanExecuteChanged();
            MoveDownLayerCommand.RaiseCanExecuteChanged();
            RenameLayerCommand.RaiseCanExecuteChanged();
        }

        private void AddLayer()
        {
            var newLayerData = new LayerData { FilePath = string.Empty };
            _parameter.Layers.Add(newLayerData);
            _parameter.SelectedLayerIndex = _parameter.Layers.Count - 1;
        }

        private bool CanRemove() => SelectedLayer != null && Layers.Count > 1;

        private void RemoveLayer()
        {
            if (SelectedLayer == null) return;

            var item = SelectedLayer;
            var index = Layers.IndexOf(item);

            string? parentGuid = item.Data.ParentGuid;

            var (startIndex, count) = GetLayerRange(index);
            var itemsToRemove = Layers.Skip(startIndex).Take(count).Select(l => l.Data).ToList();

            SelectedLayer = null;
            _parameter.SelectedLayerIndex = -1;

            _parameter.BeginUpdate();
            _suppressCollectionChanged = true;
            try
            {
                foreach (var data in itemsToRemove)
                {
                    if (!string.IsNullOrEmpty(data.ParentGuid))
                    {
                        var parent = _parameter.Layers.FirstOrDefault(l => l.Guid == data.ParentGuid);
                        if (parent != null && !itemsToRemove.Contains(parent))
                        {
                            if (parent.VisibleParts != null && data.VisibleParts != null)
                            {
                                var newSet = new HashSet<int>(parent.VisibleParts);
                                newSet.UnionWith(data.VisibleParts);
                                parent.VisibleParts = newSet;

                                var model = _loader.Load(parent.FilePath);
                                if (model.Vertices.Length > 0)
                                {
                                    parent.Thumbnail = ThumbnailUtil.CreateThumbnail(model, 64, 64, 0, -1, parent.VisibleParts);
                                }
                            }
                        }
                    }

                    _parameter.Layers.Remove(data);
                }
            }
            finally
            {
                _parameter.EndUpdate();
                _suppressCollectionChanged = false;
            }

            SyncLayers();

            LayerItemViewModel? nextSelection = null;

            if (!string.IsNullOrEmpty(parentGuid))
            {
                nextSelection = Layers.FirstOrDefault(l => l.Data.Guid == parentGuid);
            }

            if (nextSelection == null && Layers.Count > 0)
            {
                int newIndex = Math.Min(index, Layers.Count - 1);
                nextSelection = Layers[newIndex];
            }

            if (nextSelection != null)
            {
                SelectedLayer = nextSelection;
            }
            else
            {
                SelectedLayer = null;
                _parameter.FilePath = string.Empty;
            }
        }

        private void RenameLayer()
        {
            if (SelectedLayer != null)
            {
                SelectedLayer.IsEditing = true;
            }
        }

        private (int index, int count) GetLayerRange(int index)
        {
            if (index < 0 || index >= Layers.Count) return (index, 0);
            var item = Layers[index];
            int count = 1;
            for (int i = index + 1; i < Layers.Count; i++)
            {
                if (Layers[i].Depth > item.Depth)
                    count++;
                else
                    break;
            }
            return (index, count);
        }

        private bool CanMoveUp()
        {
            if (SelectedLayer == null) return false;
            int idx = Layers.IndexOf(SelectedLayer);
            if (idx <= 0) return false;

            var prevItem = Layers[idx - 1];
            if (prevItem.Depth < SelectedLayer.Depth) return false;

            return true;
        }

        private void MoveUp()
        {
            if (SelectedLayer == null) return;
            var idx = Layers.IndexOf(SelectedLayer);
            if (idx <= 0) return;

            var (currentStart, currentCount) = GetLayerRange(idx);

            int prevSiblingIdx = -1;
            for (int i = idx - 1; i >= 0; i--)
            {
                if (Layers[i].Depth == SelectedLayer.Depth)
                {
                    prevSiblingIdx = i;
                    break;
                }
                if (Layers[i].Depth < SelectedLayer.Depth) break;
            }

            if (prevSiblingIdx == -1) return;

            var (_, prevCount) = GetLayerRange(prevSiblingIdx);

            _parameter.BeginUpdate();
            _suppressCollectionChanged = true;
            try
            {
                for (int i = 0; i < currentCount; i++)
                {
                    _parameter.Layers.Move(currentStart + i, prevSiblingIdx + i);
                }
            }
            finally
            {
                _parameter.EndUpdate();
                _suppressCollectionChanged = false;
            }

            SyncLayers();
            _parameter.SelectedLayerIndex = prevSiblingIdx;
            UpdateCommands();
        }

        private bool CanMoveDown()
        {
            if (SelectedLayer == null) return false;
            int idx = Layers.IndexOf(SelectedLayer);
            if (idx < 0) return false;

            var (currentStart, currentCount) = GetLayerRange(idx);
            int nextIdx = currentStart + currentCount;

            if (nextIdx >= Layers.Count) return false;

            if (Layers[nextIdx].Depth < SelectedLayer.Depth) return false;

            return true;
        }

        private void MoveDown()
        {
            if (SelectedLayer == null) return;
            int idx = Layers.IndexOf(SelectedLayer);

            var (currentStart, currentCount) = GetLayerRange(idx);

            int nextIdx = currentStart + currentCount;
            if (nextIdx >= Layers.Count) return;

            if (Layers[nextIdx].Depth != SelectedLayer.Depth) return;

            var (_, nextCount) = GetLayerRange(nextIdx);

            _parameter.BeginUpdate();
            _suppressCollectionChanged = true;
            try
            {
                for (int i = 0; i < nextCount; i++)
                {
                    _parameter.Layers.Move(currentStart + currentCount + i, currentStart + i);
                }
            }
            finally
            {
                _parameter.EndUpdate();
                _suppressCollectionChanged = false;
            }

            SyncLayers();
            _parameter.SelectedLayerIndex = currentStart + nextCount;
            UpdateCommands();
        }

        public void Dispose()
        {
            foreach (var layer in Layers)
            {
                layer.Dispose();
            }
            PropertyChangedEventManager.RemoveHandler(_parameter, OnParameterPropertyChanged, string.Empty);
            CollectionChangedEventManager.RemoveHandler(_parameter.Layers, OnLayersCollectionChanged);
        }
    }
}