using ObjLoader.Core;
using ObjLoader.Localization;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Settings;
using ObjLoader.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using YukkuriMovieMaker.Commons;
using System.Windows;

namespace ObjLoader.ViewModels
{
    internal class SettingButtonViewModel : Bindable
    {
        private readonly ObjLoaderParameter _parameter;
        private LayerWindow? _layerWindow;

        public ActionCommand OpenSettingWindowCommand { get; }
        public ActionCommand OpenLayerWindowCommand { get; }

        public SettingButtonViewModel(ObjLoaderParameter parameter)
        {
            _parameter = parameter;

            PropertyChangedEventManager.AddHandler(_parameter, OnParameterPropertyChanged, string.Empty);

            OpenSettingWindowCommand = new ActionCommand(
                _ => true,
                _ => OpenSettingWindow()
            );

            OpenLayerWindowCommand = new ActionCommand(
                _ => !string.IsNullOrEmpty(_parameter.FilePath) || _parameter.Layers.Count > 0,
                _ => OpenLayerWindow()
            );

            VersionChecker.CheckVersion();
        }

        private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ObjLoaderParameter.FilePath) || e.PropertyName == nameof(ObjLoaderParameter.Layers))
            {
                OpenLayerWindowCommand.RaiseCanExecuteChanged();
            }
        }

        private void OpenSettingWindow()
        {
            var memento = PluginSettings.Instance.CreateMemento();
            var window = new SettingWindow
            {
                DataContext = new SettingWindowViewModel(PluginSettings.Instance)
            };

            if (window.ShowDialog() != true)
            {
                PluginSettings.Instance.RestoreMemento(memento);
            }
        }

        private void OpenLayerWindow()
        {
            if (_layerWindow != null)
            {
                _layerWindow.Activate();
                if (_layerWindow.WindowState == WindowState.Minimized)
                {
                    _layerWindow.WindowState = WindowState.Normal;
                }
                return;
            }

            if (_parameter.Layers.Count == 0)
            {
                _parameter.Layers.Add(new LayerData { FilePath = _parameter.FilePath });
                _parameter.SelectedLayerIndex = 0;
            }

            _layerWindow = new LayerWindow
            {
                DataContext = new LayerWindowViewModel(_parameter),
                Owner = Application.Current.MainWindow
            };
            _layerWindow.Closed += (s, e) => _layerWindow = null;
            _layerWindow.Show();
        }
    }

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

            foreach (var layer in _parameter.Layers)
            {
                Layers.Add(new LayerItemViewModel(layer, _loader));
            }

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

            AddLayerCommand = new ActionCommand(_ => true, _ => AddLayer());
            RemoveLayerCommand = new ActionCommand(_ => CanRemove(), _ => RemoveLayer());
            MoveUpLayerCommand = new ActionCommand(_ => CanMoveUp(), _ => MoveUp());
            MoveDownLayerCommand = new ActionCommand(_ => CanMoveDown(), _ => MoveDown());
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

            var newItem = new LayerItemViewModel(newLayerData, _loader);
            Layers.Add(newItem);

            SelectedLayer = newItem;
        }

        private bool CanRemove() => SelectedLayer != null && Layers.Count > 1;

        private void RemoveLayer()
        {
            if (SelectedLayer == null) return;

            var item = SelectedLayer;
            var index = Layers.IndexOf(item);

            Layers.Remove(item);
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

                _parameter.FilePath = currentLayer.FilePath;
                _parameter.BaseColor = currentLayer.BaseColor;
                _parameter.IsLightEnabled = currentLayer.IsLightEnabled;
                _parameter.Projection = currentLayer.Projection;

                _parameter.X.CopyFrom(currentLayer.X);
                _parameter.Y.CopyFrom(currentLayer.Y);
                _parameter.Z.CopyFrom(currentLayer.Z);
                _parameter.Scale.CopyFrom(currentLayer.Scale);
                _parameter.RotationX.CopyFrom(currentLayer.RotationX);
                _parameter.RotationY.CopyFrom(currentLayer.RotationY);
                _parameter.RotationZ.CopyFrom(currentLayer.RotationZ);
                _parameter.Fov.CopyFrom(currentLayer.Fov);
                _parameter.LightX.CopyFrom(currentLayer.LightX);
                _parameter.LightY.CopyFrom(currentLayer.LightY);
                _parameter.LightZ.CopyFrom(currentLayer.LightZ);
                _parameter.WorldId.CopyFrom(currentLayer.WorldId);

                _parameter.SelectedLayerIndex = newIndex;
            }

            UpdateCommands();
        }

        private bool CanMoveUp() => SelectedLayer != null && Layers.IndexOf(SelectedLayer) > 0;

        private void MoveUp()
        {
            if (SelectedLayer == null) return;
            var index = Layers.IndexOf(SelectedLayer);
            if (index <= 0) return;

            Layers.Move(index, index - 1);
            _parameter.Layers.Move(index, index - 1);
            _parameter.SelectedLayerIndex = index - 1;

            UpdateCommands();
        }

        private bool CanMoveDown() => SelectedLayer != null && Layers.IndexOf(SelectedLayer) < Layers.Count - 1;

        private void MoveDown()
        {
            if (SelectedLayer == null) return;
            var index = Layers.IndexOf(SelectedLayer);
            if (index < 0 || index >= Layers.Count - 1) return;

            Layers.Move(index, index + 1);
            _parameter.Layers.Move(index, index + 1);
            _parameter.SelectedLayerIndex = index + 1;

            UpdateCommands();
        }

        public void Dispose()
        {
            PropertyChangedEventManager.RemoveHandler(_parameter, OnParameterPropertyChanged, string.Empty);
        }
    }

    internal class LayerItemViewModel : Bindable
    {
        private readonly ObjModelLoader _loader;
        public LayerData Data { get; }

        public string Name => string.IsNullOrEmpty(Data.FilePath) ? Texts.Layer_New : Path.GetFileName(Data.FilePath);

        private byte[] _thumbnail = Array.Empty<byte>();
        public byte[] Thumbnail
        {
            get => _thumbnail;
            set => Set(ref _thumbnail, value);
        }

        public LayerItemViewModel(LayerData data, ObjModelLoader loader)
        {
            Data = data;
            _loader = loader;
            UpdateThumbnail();
        }

        public void UpdateThumbnail()
        {
            if (!string.IsNullOrEmpty(Data.FilePath) && File.Exists(Data.FilePath))
            {
                Thumbnail = _loader.GetThumbnail(Data.FilePath);
            }
            else
            {
                Thumbnail = Array.Empty<byte>();
            }
            OnPropertyChanged(nameof(Name));
        }
    }
}