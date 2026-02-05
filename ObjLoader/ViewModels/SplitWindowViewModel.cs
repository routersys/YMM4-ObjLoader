using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Localization;
using ObjLoader.Plugin;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using Microsoft.Win32;
using YukkuriMovieMaker.Commons;
using Vector3 = System.Numerics.Vector3;
using ObjLoader.Services.Layers;
using ObjLoader.Services.Camera;
using ObjLoader.Services.Rendering;
using ObjLoader.Services.Models;

namespace ObjLoader.ViewModels
{
    internal class SplitWindowViewModel : Bindable, IDisposable
    {
        private readonly ObjLoaderParameter _parameter;
        private readonly RenderService _renderService;
        private readonly PreviewCameraService _cameraService;
        private readonly ModelManagementService _modelService;
        private readonly LayerManipulationService _layerService;
        private Color _themeColor = Colors.White;

        private GpuResourceCacheItem? _modelResource;
        private ObjModel? _currentModel;
        private double _modelHeight;
        private double _modelScale = 1.0;

        private int _viewportWidth = 100;
        private int _viewportHeight = 100;
        private bool _isNarrowMode;

        private PartItem? _selectedPart;

        public WriteableBitmap? SceneImage => _renderService.SceneImage;

        public ObservableCollection<PartItem> Parts { get; } = new ObservableCollection<PartItem>();

        public List<PartItem> SelectedPartItems { get; set; } = new List<PartItem>();

        public PartItem? SelectedPart
        {
            get => _selectedPart;
            set
            {
                if (Set(ref _selectedPart, value))
                {
                    OnPropertyChanged(nameof(IsPartSelected));
                    UpdateMaterialProperties();
                    UpdateFocus();
                    SavePresetCommand.RaiseCanExecuteChanged();
                    LoadPresetCommand.RaiseCanExecuteChanged();
                    ResetMaterialCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsPartSelected => _selectedPart != null && _selectedPart.Index != -1;
        public bool IsNarrowMode
        {
            get => _isNarrowMode;
            set => Set(ref _isNarrowMode, value);
        }

        public bool IsInteracting => _cameraService.IsInteracting;

        public double SelectedPartRoughness
        {
            get => GetMaterialValue(m => m.Roughness, p => (double)p.Roughness, 0.5);
            set => SetMaterialValue((m, v) => m.Roughness = v, value);
        }

        public double SelectedPartMetallic
        {
            get => GetMaterialValue(m => m.Metallic, p => (double)p.Metallic, 0.0);
            set => SetMaterialValue((m, v) => m.Metallic = v, value);
        }

        public Color SelectedPartBaseColor
        {
            get => GetMaterialValue(
                m => m.BaseColor,
                p =>
                {
                    var c = p.BaseColor;
                    return Color.FromScRgb(c.W, c.X, c.Y, c.Z);
                },
                Colors.White);
            set => SetMaterialValue((m, v) => m.BaseColor = v, value);
        }

        public ActionCommand AddToLayerCommand { get; }
        public ActionCommand SavePresetCommand { get; }
        public ActionCommand LoadPresetCommand { get; }
        public ActionCommand ResetMaterialCommand { get; }

        public SplitWindowViewModel(ObjLoaderParameter parameter)
        {
            _parameter = parameter;
            _renderService = new RenderService();
            _cameraService = new PreviewCameraService();
            _modelService = new ModelManagementService();
            _layerService = new LayerManipulationService();

            AddToLayerCommand = new ActionCommand(_ => true, AddToLayer);
            SavePresetCommand = new ActionCommand(_ => IsPartSelected, SavePreset);
            LoadPresetCommand = new ActionCommand(_ => IsPartSelected, LoadPreset);
            ResetMaterialCommand = new ActionCommand(_ => IsPartSelected, ResetMaterial);

            _cameraService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PreviewCameraService.IsInteracting))
                {
                    OnPropertyChanged(nameof(IsInteracting));
                }
            };

            _renderService.Initialize();
            LoadModel();

            _parameter.PropertyChanged += OnParameterPropertyChanged;
        }

        private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ObjLoaderParameter.FilePath))
            {
                var layer = GetCurrentLayer();
                layer?.PartMaterials.Clear();
                LoadModel();
            }
            else if (e.PropertyName == nameof(ObjLoaderParameter.SelectedLayerIndex))
            {
                LoadModel();
            }
        }

        public void Resize(int width, int height)
        {
            _viewportWidth = width;
            _viewportHeight = height;
            IsNarrowMode = width < 600;
            _renderService.Resize(width, height);
            _cameraService.Resize(width, height);
            OnPropertyChanged(nameof(SceneImage));
            UpdateVisuals();
        }

        public void UpdateThemeColor(Color color)
        {
            _themeColor = color;
            UpdateVisuals();
        }

        public void Zoom(int delta)
        {
            _cameraService.Zoom(delta);
            UpdateVisuals();
        }

        public void StartInteraction(Point pos, MouseButton button)
        {
            _cameraService.StartInteraction(pos, button);
        }

        public void MoveInteraction(Point pos, bool left, bool middle, bool right)
        {
            if (_cameraService.MoveInteraction(pos, left, middle, right))
            {
                UpdateVisuals();
            }
        }

        public void EndInteraction()
        {
            _cameraService.EndInteraction();
        }

        private void UpdateMaterialProperties()
        {
            OnPropertyChanged(nameof(SelectedPartRoughness));
            OnPropertyChanged(nameof(SelectedPartMetallic));
            OnPropertyChanged(nameof(SelectedPartBaseColor));
        }

        private T GetMaterialValue<T>(Func<PartMaterialData, T> materialSelector, Func<ModelPart, T> modelSelector, T defaultValue)
        {
            if (_selectedPart == null || _selectedPart.Index == -1) return defaultValue;

            var layer = GetCurrentLayer();
            if (layer != null && layer.PartMaterials.TryGetValue(_selectedPart.Index, out var material))
            {
                return materialSelector(material);
            }

            if (_currentModel != null && _selectedPart.Index >= 0 && _selectedPart.Index < _currentModel.Parts.Count)
            {
                return modelSelector(_currentModel.Parts[_selectedPart.Index]);
            }

            return defaultValue;
        }

        private void SetMaterialValue<T>(Action<PartMaterialData, T> setter, T value)
        {
            if (_selectedPart == null || _selectedPart.Index == -1) return;
            var layer = GetCurrentLayer();
            if (layer != null)
            {
                if (!layer.PartMaterials.TryGetValue(_selectedPart.Index, out var material))
                {
                    material = CreateMaterialFromModel(_selectedPart.Index);
                    layer.PartMaterials[_selectedPart.Index] = material;
                }
                setter(material, value);
                _parameter.ForceUpdate();
                UpdateVisuals();
                OnPropertyChanged(string.Empty);
            }
        }

        private PartMaterialData CreateMaterialFromModel(int partIndex)
        {
            var data = new PartMaterialData { Roughness = 0.5, Metallic = 0.0, BaseColor = Colors.White };

            if (_currentModel != null && partIndex >= 0 && partIndex < _currentModel.Parts.Count)
            {
                var part = _currentModel.Parts[partIndex];
                data.Roughness = part.Roughness;
                data.Metallic = part.Metallic;
                var c = part.BaseColor;
                data.BaseColor = Color.FromScRgb(c.W, c.X, c.Y, c.Z);
            }
            return data;
        }

        private void ResetMaterial(object? _)
        {
            if (_selectedPart == null || _selectedPart.Index == -1) return;

            var layer = GetCurrentLayer();
            if (layer != null)
            {
                if (layer.PartMaterials.Remove(_selectedPart.Index))
                {
                    _parameter.ForceUpdate();
                    UpdateMaterialProperties();
                    UpdateVisuals();
                }
            }
        }

        private LayerData? GetCurrentLayer()
        {
            if (_parameter.SelectedLayerIndex >= 0 && _parameter.SelectedLayerIndex < _parameter.Layers.Count)
            {
                return _parameter.Layers[_parameter.SelectedLayerIndex];
            }
            return null;
        }

        private void SavePreset(object? _)
        {
            if (_selectedPart == null || _selectedPart.Index == -1) return;

            var filter = $"{Texts.SplitWindow_MaterialPreset} (*.json)|*.json";
            var dlg = new SaveFileDialog { Filter = filter };
            if (dlg.ShowDialog() == true)
            {
                var preset = new MaterialPreset
                {
                    Roughness = SelectedPartRoughness,
                    Metallic = SelectedPartMetallic,
                    BaseColor = SelectedPartBaseColor
                };
                File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(preset));
            }
        }

        private void LoadPreset(object? _)
        {
            if (_selectedPart == null || _selectedPart.Index == -1) return;

            var filter = $"{Texts.SplitWindow_MaterialPreset} (*.json)|*.json";
            var dlg = new OpenFileDialog { Filter = filter };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dlg.FileName);
                    var preset = JsonSerializer.Deserialize<MaterialPreset>(json);
                    if (preset != null)
                    {
                        SelectedPartRoughness = preset.Roughness;
                        SelectedPartMetallic = preset.Metallic;
                        SelectedPartBaseColor = preset.BaseColor;
                    }
                }
                catch { }
            }
        }

        private void AddToLayer(object? parameter)
        {
            var targets = new List<PartItem>();

            if (parameter is PartItem clickedItem)
            {
                targets.Add(clickedItem);
                if (SelectedPartItems.Contains(clickedItem))
                {
                    foreach (var item in SelectedPartItems)
                    {
                        if (!targets.Contains(item)) targets.Add(item);
                    }
                }
            }
            else
            {
                if (SelectedPartItems.Count > 0)
                {
                    foreach (var item in SelectedPartItems) targets.Add(item);
                }
                else if (_selectedPart != null)
                {
                    targets.Add(_selectedPart);
                }
            }

            _layerService.AddToLayer(_parameter, _modelResource, _currentModel, targets, Parts);
        }

        private void UpdateFocus()
        {
            if (_selectedPart == null || _selectedPart.Index == -1)
            {
                _cameraService.UpdateFocus(new Vector3(0, (float)(_modelHeight / 2.0), 0), _modelScale * 2.5);
            }
            else
            {
                _cameraService.UpdateFocus(_selectedPart.Center, _selectedPart.Radius * 2.5);
            }
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_renderService.SceneImage == null || _modelResource == null) return;

            var camPos = _cameraService.GetCameraPosition();
            var view = _cameraService.GetViewMatrix();
            var proj = _cameraService.GetProjectionMatrix();

            HashSet<int>? visibleParts = null;
            if (_selectedPart != null && _selectedPart.Index != -1)
            {
                visibleParts = new HashSet<int> { _selectedPart.Index };
            }

            var currentLayerData = GetCurrentLayer();
            var layerData = currentLayerData != null ? currentLayerData.Clone() : new LayerData();
            if (currentLayerData != null)
            {
                layerData.PartMaterials = currentLayerData.PartMaterials;
            }
            layerData.VisibleParts = visibleParts;

            var layers = new List<LayerRenderData>
            {
                new LayerRenderData
                {
                    Resource = _modelResource,
                    X = 0, Y = 0, Z = 0,
                    Scale = 100,
                    Rx = 0, Ry = 0, Rz = 0,
                    BaseColor = Colors.White,
                    LightEnabled = true,
                    WorldId = 0,
                    HeightOffset = _modelHeight / 2.0,
                    VisibleParts = visibleParts,
                    Data = layerData
                }
            };

            _renderService.Render(
                layers,
                view,
                proj,
                camPos,
                _themeColor,
                false,
                true,
                true,
                _modelScale,
                _cameraService.IsInteracting);
        }

        private void LoadModel()
        {
            _modelResource?.Dispose();
            _modelResource = null;
            _currentModel = null;
            Parts.Clear();

            var result = _modelService.LoadModel(_parameter.FilePath, _renderService, _parameter.SelectedLayerIndex, _parameter.Layers);

            _currentModel = result.Model;
            _modelResource = result.Resource;
            _modelScale = result.Scale;
            _modelHeight = result.Height;

            foreach (var part in result.Parts)
            {
                Parts.Add(part);
            }

            if (Parts.Count > 0)
            {
                SelectedPart = Parts[0];
            }
            else
            {
                UpdateFocus();
            }
        }

        public void Dispose()
        {
            _parameter.PropertyChanged -= OnParameterPropertyChanged;
            _modelResource?.Dispose();
            _renderService.Dispose();
        }
    }
}