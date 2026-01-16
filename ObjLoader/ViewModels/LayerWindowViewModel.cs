using ObjLoader.Core;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
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
                    }

                    UpdateCommands();
                }
            }
        }

        public ActionCommand AddLayerCommand { get; }
        public ActionCommand RemoveLayerCommand { get; }
        public ActionCommand MoveUpLayerCommand { get; }
        public ActionCommand MoveDownLayerCommand { get; }

        public LayerWindowViewModel(ObjLoaderParameter parameter)
        {
            _parameter = parameter;

            SyncLayers();

            if (_parameter.SelectedLayerIndex >= 0 && _parameter.SelectedLayerIndex < Layers.Count)
            {
                _selectedLayer = Layers[_parameter.SelectedLayerIndex];
                OnPropertyChanged(nameof(SelectedLayer));
            }
            else if (Layers.Count > 0)
            {
                _selectedLayer = Layers[0];
                OnPropertyChanged(nameof(SelectedLayer));
            }

            PropertyChangedEventManager.AddHandler(_parameter, OnParameterPropertyChanged, string.Empty);
            CollectionChangedEventManager.AddHandler(_parameter.Layers, OnLayersCollectionChanged);

            AddLayerCommand = new ActionCommand(_ => true, _ => AddLayer());
            RemoveLayerCommand = new ActionCommand(_ => CanRemove(), _ => RemoveLayer());
            MoveUpLayerCommand = new ActionCommand(_ => CanMoveUp(), _ => MoveUp());
            MoveDownLayerCommand = new ActionCommand(_ => CanMoveDown(), _ => MoveDown());
        }

        private void SyncLayers()
        {
            Layers.Clear();
            var dict = _parameter.Layers.ToDictionary(x => x.Guid);
            foreach (var layer in _parameter.Layers)
            {
                int depth = 0;
                var current = layer;
                while (!string.IsNullOrEmpty(current.ParentGuid) && dict.TryGetValue(current.ParentGuid, out var parent))
                {
                    depth++;
                    current = parent;
                    if (depth > 20) break;
                }

                Layers.Add(new LayerItemViewModel(layer, _loader) { Depth = depth });
            }
        }

        private void OnLayersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SyncLayers();
                    if (_parameter.SelectedLayerIndex >= 0 && _parameter.SelectedLayerIndex < Layers.Count)
                    {
                        SelectedLayer = Layers[_parameter.SelectedLayerIndex];
                    }
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
                }
            }
        }

        private void UpdateCommands()
        {
            RemoveLayerCommand.RaiseCanExecuteChanged();
            MoveUpLayerCommand.RaiseCanExecuteChanged();
            MoveDownLayerCommand.RaiseCanExecuteChanged();
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

            _parameter.Layers.Remove(item.Data);

            int newIndex = 0;
            if (Layers.Count > 0)
            {
                newIndex = Math.Min(index, Layers.Count - 1);
                SelectedLayer = Layers[newIndex];
            }
            else
            {
                SelectedLayer = null;
                _parameter.FilePath = string.Empty;
            }

            if (Layers.Count > 0)
            {
                var currentLayer = Layers[newIndex].Data;
                _parameter.SelectedLayerIndex = newIndex;
            }
        }

        private bool CanMoveUp() => SelectedLayer != null && Layers.IndexOf(SelectedLayer) > 0;

        private void MoveUp()
        {
            if (SelectedLayer == null) return;
            var index = Layers.IndexOf(SelectedLayer);
            if (index <= 0) return;

            _parameter.Layers.Move(index, index - 1);
            _parameter.SelectedLayerIndex = index - 1;
        }

        private bool CanMoveDown() => SelectedLayer != null && Layers.IndexOf(SelectedLayer) < Layers.Count - 1;

        private void MoveDown()
        {
            if (SelectedLayer == null) return;
            var index = Layers.IndexOf(SelectedLayer);
            if (index < 0 || index >= Layers.Count - 1) return;

            _parameter.Layers.Move(index, index + 1);
            _parameter.SelectedLayerIndex = index + 1;
        }

        public void Dispose()
        {
            PropertyChangedEventManager.RemoveHandler(_parameter, OnParameterPropertyChanged, string.Empty);
            CollectionChangedEventManager.RemoveHandler(_parameter.Layers, OnLayersCollectionChanged);
        }
    }
}