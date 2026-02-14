using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Plugin;
using ObjLoader.Utilities;
using ObjLoader.ViewModels.Splitter;
using System.Collections.ObjectModel;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Services.Layers
{
    internal class LayerManipulationService
    {
        public void AddToLayer(ObjLoaderParameter parameter, GpuResourceCacheItem? modelResource, ObjModel? currentModel, List<PartItem> targetParts, ObservableCollection<PartItem> allParts)
        {
            if (modelResource == null || currentModel == null) return;

            var targetList = targetParts.Where(t => t.Index != -1).ToList();
            if (targetList.Count == 0) return;

            LayerData? sourceLayer = null;
            var currentLayerIndex = parameter.SelectedLayerIndex;
            if (currentLayerIndex >= 0 && currentLayerIndex < parameter.Layers.Count)
            {
                var layer = parameter.Layers[currentLayerIndex];
                if (IsLayerContainsAnyTarget(layer, targetList))
                {
                    sourceLayer = layer;
                }
            }

            if (sourceLayer == null)
            {
                foreach (var layer in parameter.Layers)
                {
                    if (layer.IsVisible && IsLayerContainsAnyTarget(layer, targetList))
                    {
                        sourceLayer = layer;
                        break;
                    }
                }
            }

            if (sourceLayer == null)
            {
                foreach (var layer in parameter.Layers)
                {
                    if (IsLayerContainsAnyTarget(layer, targetList))
                    {
                        sourceLayer = layer;
                        break;
                    }
                }
            }

            if (sourceLayer == null) return;

            if (sourceLayer.VisibleParts == null)
            {
                sourceLayer.VisibleParts = new HashSet<int>(Enumerable.Range(0, modelResource.Parts.Length));
            }

            var indicesToMove = new HashSet<int>();
            foreach (var t in targetList)
            {
                if (sourceLayer.VisibleParts.Contains(t.Index))
                {
                    indicesToMove.Add(t.Index);
                }
            }

            if (indicesToMove.Count > 0)
            {
                var newVisibleParts = new HashSet<int>(sourceLayer.VisibleParts);
                foreach (var idx in indicesToMove)
                {
                    newVisibleParts.Remove(idx);
                }
                sourceLayer.VisibleParts = newVisibleParts;

                sourceLayer.Thumbnail = ThumbnailUtil.CreateThumbnail(currentModel, 64, 64, 0, -1, sourceLayer.VisibleParts);

                var partsToRemove = allParts.Where(p => p.Index != -1 && indicesToMove.Contains(p.Index)).ToList();
                foreach (var p in partsToRemove)
                {
                    allParts.Remove(p);
                }

                var newLayer = sourceLayer.Clone();
                newLayer.X = new Animation(0, -100000, 100000);
                newLayer.Y = new Animation(0, -100000, 100000);
                newLayer.Z = new Animation(0, -100000, 100000);
                newLayer.Scale = new Animation(100, 0, 100000);
                newLayer.RotationX = new Animation(0, -36000, 36000);
                newLayer.RotationY = new Animation(0, -36000, 36000);
                newLayer.RotationZ = new Animation(0, -36000, 36000);

                if (targetList.Count == 1)
                {
                    newLayer.Name = targetList[0].Name;
                }
                else
                {
                    newLayer.Name = $"{targetList[0].Name} + {targetList.Count - 1}";
                }

                newLayer.VisibleParts = indicesToMove;
                newLayer.Guid = Guid.NewGuid().ToString();
                newLayer.ParentGuid = sourceLayer.Guid;

                newLayer.Thumbnail = ThumbnailUtil.CreateThumbnail(currentModel, 64, 64, 0, -1, newLayer.VisibleParts);

                int sourceIndex = -1;
                for (int i = 0; i < parameter.Layers.Count; i++)
                {
                    if (parameter.Layers[i].Guid == sourceLayer.Guid)
                    {
                        sourceIndex = i;
                        break;
                    }
                }

                int insertIndex;
                if (sourceIndex != -1)
                {
                    insertIndex = sourceIndex + 1;
                    while (insertIndex < parameter.Layers.Count)
                    {
                        if (parameter.Layers[insertIndex].ParentGuid == sourceLayer.Guid)
                        {
                            insertIndex++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    insertIndex = parameter.Layers.Count;
                }

                parameter.Layers.Insert(insertIndex, newLayer);

                parameter.ForceUpdate();
            }
        }

        private bool IsLayerContainsAnyTarget(LayerData layer, List<PartItem> targets)
        {
            if (layer.VisibleParts == null) return true;

            foreach (var t in targets)
            {
                if (layer.VisibleParts.Contains(t.Index)) return true;
            }
            return false;
        }
    }
}