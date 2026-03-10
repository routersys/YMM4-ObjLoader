using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Models;
using ObjLoader.Core.Timeline;
using ObjLoader.Infrastructure;
using ObjLoader.Localization;
using ObjLoader.Parsers;
using ObjLoader.Services.Rendering;
using ObjLoader.Services.Textures;
using ObjLoader.Settings;
using ObjLoader.Utilities;
using ObjLoader.ViewModels.Splitter;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Vortice.Direct3D11;
using Vector3 = System.Numerics.Vector3;
using ObjLoader.Utilities.Logging;
using ObjLoader.Rendering.Mathematics;

namespace ObjLoader.Services.Models
{
    internal class ModelManagementService
    {
        private readonly ObjModelLoader _loader = new ObjModelLoader();
        private readonly TextureService _textureService = new TextureService();
        private string? _lastTrackingKey;

        public unsafe ModelLoadResult LoadModel(string path, RenderService renderService, int selectedLayerIndex, IList<LayerData> layers)
        {
            var result = new ModelLoadResult();

            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;

            var modelSettings = ModelSettings.Instance;
            try
            {
                var fileInfo = new FileInfo(path);
                if (!modelSettings.IsFileSizeAllowed(fileInfo.Length))
                {
                    long sizeMB = fileInfo.Length / (1024L * 1024L);
                    string message = string.Format(
                        Texts.FileSizeExceeded,
                        Path.GetFileName(path),
                        sizeMB,
                        modelSettings.MaxFileSizeMB);
                    UserNotification.ShowWarning(message, Texts.ResourceLimitTitle);
                    return result;
                }
            }
            catch (IOException ex)
            {
                Logger<ModelManagementService>.Instance.Error("Failed to check file size (IO)", ex);
                return result;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger<ModelManagementService>.Instance.Error("Failed to check file size (access denied)", ex);
                return result;
            }

            var model = _loader.Load(path);
            if (model.Vertices.Length == 0) return result;

            result.Model = model;

            ID3D11Buffer? vb = null;
            ID3D11Buffer? ib = null;
            ID3D11ShaderResourceView?[]? partTextures = null;
            bool success = false;
            long gpuBytes = 0;

            try
            {
                int vertexBufferSize = model.Vertices.Length * Unsafe.SizeOf<ObjVertex>();
                var vDesc = new BufferDescription(vertexBufferSize, BindFlags.VertexBuffer, ResourceUsage.Immutable);
                fixed (ObjVertex* p = model.Vertices) vb = renderService.Device!.CreateBuffer(vDesc, new SubresourceData(p));
                gpuBytes += vertexBufferSize;

                int indexBufferSize = model.Indices.Length * sizeof(int);
                var iDesc = new BufferDescription(indexBufferSize, BindFlags.IndexBuffer, ResourceUsage.Immutable);
                fixed (int* p = model.Indices) ib = renderService.Device.CreateBuffer(iDesc, new SubresourceData(p));
                gpuBytes += indexBufferSize;

                var (globalBox, parts) = BoundingBoxUtility.CalculateBounds(model);
                model.LocalBoundingBox = globalBox;
                model.Parts = parts.ToList();
                partTextures = new ID3D11ShaderResourceView?[parts.Length];

                for (int i = 0; i < parts.Length; i++)
                {
                    if (!File.Exists(parts[i].TexturePath)) continue;

                    try
                    {
                        var (srv, texGpuBytes) = _textureService.CreateShaderResourceView(parts[i].TexturePath, renderService.Device);
                        partTextures[i] = srv;
                        gpuBytes += texGpuBytes;
                    }
                    catch (FileNotFoundException ex)
                    {
                        Logger<ModelManagementService>.Instance.Warning($"Texture file not found: {parts[i].TexturePath}", ex);
                    }
                    catch (IOException ex)
                    {
                        Logger<ModelManagementService>.Instance.Warning($"IO error loading texture {parts[i].TexturePath}", ex);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Logger<ModelManagementService>.Instance.Warning($"Access denied for texture {parts[i].TexturePath}", ex);
                    }
                    catch (InvalidDataException ex)
                    {
                        Logger<ModelManagementService>.Instance.Warning($"Corrupt texture file {parts[i].TexturePath}", ex);
                    }
                }

                if (!modelSettings.IsGpuMemoryPerModelAllowed(gpuBytes))
                {
                    long gpuMB = gpuBytes / (1024L * 1024L);
                    string message = string.Format(
                        Texts.GpuMemoryExceeded,
                        Path.GetFileName(path),
                        gpuMB,
                        modelSettings.MaxGpuMemoryPerModelMB);
                    UserNotification.ShowWarning(message, Texts.ResourceLimitTitle);
                    return result;
                }

                result.Resource = new GpuResourceCacheItem(renderService.Device, vb, ib, model.Indices.Length, parts, partTextures, model.ModelCenter, model.ModelScale, globalBox, gpuBytes);

                if (!string.IsNullOrEmpty(_lastTrackingKey))
                {
                    ResourceTracker.Instance.Unregister(_lastTrackingKey);
                }

                string trackingKey = $"ModelMgmt:{path}";
                ResourceTracker.Instance.Register(trackingKey, "GpuResourceCacheItem:Preview", result.Resource, gpuBytes);
                _lastTrackingKey = trackingKey;

                success = true;
            }
            finally
            {
                if (!success)
                {
                    if (partTextures != null)
                    {
                        for (int i = 0; i < partTextures.Length; i++)
                        {
                            SafeDispose(partTextures[i]);
                            partTextures[i] = null;
                        }
                    }
                    SafeDispose(ib);
                    SafeDispose(vb);
                }
            }

            double minX = (model.LocalBoundingBox.Min.X - model.ModelCenter.X) * model.ModelScale;
            double minY = (model.LocalBoundingBox.Min.Y - model.ModelCenter.Y) * model.ModelScale;
            double minZ = (model.LocalBoundingBox.Min.Z - model.ModelCenter.Z) * model.ModelScale;
            double maxX = (model.LocalBoundingBox.Max.X - model.ModelCenter.X) * model.ModelScale;
            double maxY = (model.LocalBoundingBox.Max.Y - model.ModelCenter.Y) * model.ModelScale;
            double maxZ = (model.LocalBoundingBox.Max.Z - model.ModelCenter.Z) * model.ModelScale;
            double localMinY = minY;
            double localMaxY = maxY;

            result.Scale = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
            result.Height = localMaxY - localMinY;
            if (result.Scale < 0.1) result.Scale = 1.0;

            HashSet<int>? currentVisibleParts = null;
            if (selectedLayerIndex >= 0 && selectedLayerIndex < layers.Count)
            {
                var layer = layers[selectedLayerIndex];
                if (layer.FilePath == path)
                {
                    currentVisibleParts = layer.VisibleParts;
                }
            }

            result.Parts.Add(new PartItem { Name = Texts.SplitWindow_All, Index = -1, Center = new Vector3(0, (float)(result.Height / 2.0), 0), Radius = result.Scale, FaceCount = model.Indices.Length / 3 });

            var parts2 = model.Parts.ToArray();
            for (int i = 0; i < parts2.Length; i++)
            {
                if (currentVisibleParts != null && !currentVisibleParts.Contains(i)) continue;

                var part = parts2[i];
                var name = string.IsNullOrEmpty(part.Name) ? string.Format(Texts.SplitWindow_PartName, i) : part.Name;

                Vector3 center = Vector3.Zero;
                double radius = 0;

                if (part.IndexCount > 0)
                {
                    double pMinX = (part.LocalBoundingBox.Min.X - model.ModelCenter.X) * model.ModelScale;
                    double pMinY = (part.LocalBoundingBox.Min.Y - model.ModelCenter.Y) * model.ModelScale;
                    double pMinZ = (part.LocalBoundingBox.Min.Z - model.ModelCenter.Z) * model.ModelScale;
                    double pMaxX = (part.LocalBoundingBox.Max.X - model.ModelCenter.X) * model.ModelScale;
                    double pMaxY = (part.LocalBoundingBox.Max.Y - model.ModelCenter.Y) * model.ModelScale;
                    double pMaxZ = (part.LocalBoundingBox.Max.Z - model.ModelCenter.Z) * model.ModelScale;

                    center = new Vector3((float)((pMinX + pMaxX) / 2.0), (float)((pMinY + pMaxY) / 2.0) + (float)(result.Height / 2.0), (float)((pMinZ + pMaxZ) / 2.0));
                    radius = Math.Max(pMaxX - pMinX, Math.Max(pMaxY - pMinY, pMaxZ - pMinZ));
                }

                result.Parts.Add(new PartItem { Name = name, Index = i, Center = center, Radius = radius, FaceCount = part.IndexCount / 3 });
            }

            GenerateThumbnails(result.Parts, model);

            return result;
        }

        private void GenerateThumbnails(List<PartItem> partItems, ObjModel model)
        {
            var items = partItems.ToList();
            Task.Run(() =>
            {
                foreach (var partItem in items)
                {
                    int offset = partItem.Index == -1 ? 0 : model.Parts[partItem.Index].IndexOffset;
                    int count = partItem.Index == -1 ? -1 : model.Parts[partItem.Index].IndexCount;

                    var bytes = ThumbnailUtil.CreateThumbnail(model, 64, 64, offset, count);
                    if (bytes != null && bytes.Length > 0)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            using var ms = new MemoryStream(bytes);
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = ms;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            partItem.Thumbnail = bitmap;
                        }));
                    }
                }
            });
        }

        public void UnregisterTracking()
        {
            if (!string.IsNullOrEmpty(_lastTrackingKey))
            {
                ResourceTracker.Instance.Unregister(_lastTrackingKey);
                _lastTrackingKey = null;
            }
        }

        private static void SafeDispose(IDisposable? disposable)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Logger<ModelManagementService>.Instance.Error("Dispose failed", ex);
            }
        }
    }
}