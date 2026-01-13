using System.Collections.ObjectModel;
using ObjLoader.Core;
using ObjLoader.Plugin;

namespace ObjLoader.Services
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
    }
}