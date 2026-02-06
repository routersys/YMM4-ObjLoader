using System.Collections.ObjectModel;
using ObjLoader.Core;
using ObjLoader.Plugin;

namespace ObjLoader.Services.Layers
{
    public interface ILayerManager
    {
        ObservableCollection<LayerData> Layers { get; }
        int SelectedLayerIndex { get; }
        bool IsSwitchingLayer { get; }
        void Initialize(ObjLoaderParameter parameter);
        void EnsureLayers(ObjLoaderParameter parameter);
        void ChangeLayer(int newIndex, ObjLoaderParameter parameter);
        void SaveActiveLayer(ObjLoaderParameter parameter);
        void LoadSharedData(IEnumerable<LayerData> layers);
        bool SetParent(string childId, string? parentId);
        bool GetEffectiveVisibility(string layerId);
        List<string> GetAllDescendants(string layerId);
        ValidationResult ValidateHierarchy();
    }

    public class ValidationResult
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public bool IsValid => Errors.Count == 0;
    }
}