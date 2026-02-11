using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Infrastructure;
using ObjLoader.Localization;
using ObjLoader.Parsers;
using ObjLoader.Services.Rendering;
using ObjLoader.Services.Textures;
using ObjLoader.Settings;
using ObjLoader.Utilities;
using ObjLoader.ViewModels;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vector3 = System.Numerics.Vector3;

namespace ObjLoader.Services.Models
{
    internal class ModelManagementService
    {
        private readonly ObjModelLoader _loader = new ObjModelLoader();
        private readonly TextureService _textureService = new TextureService();

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModelManagementService: Failed to check file size: {ex.Message}");
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

                var parts = model.Parts.ToArray();
                partTextures = new ID3D11ShaderResourceView?[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!File.Exists(parts[i].TexturePath)) continue;

                    try
                    {
                        var bitmapSource = _textureService.Load(parts[i].TexturePath);
                        if (bitmapSource.CanFreeze && !bitmapSource.IsFrozen) bitmapSource.Freeze();
                        var conv = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);
                        int width = conv.PixelWidth;
                        int height = conv.PixelHeight;
                        int stride = width * 4;
                        var pixels = new byte[stride * height];
                        conv.CopyPixels(pixels, stride, 0);
                        var tDesc = new Texture2DDescription { Width = width, Height = height, MipLevels = 1, ArraySize = 1, Format = Format.B8G8R8A8_UNorm, SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource };
                        fixed (byte* p = pixels)
                        {
                            using var t = renderService.Device.CreateTexture2D(tDesc, new[] { new SubresourceData(p, stride) });
                            partTextures[i] = renderService.Device.CreateShaderResourceView(t);
                        }
                        gpuBytes += (long)width * height * 4;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ModelManagementService: Failed to load texture {parts[i].TexturePath}: {ex.Message}");
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

                result.Resource = new GpuResourceCacheItem(renderService.Device, vb, ib, model.Indices.Length, parts, partTextures, model.ModelCenter, model.ModelScale, gpuBytes);

                string trackingKey = $"ModelMgmt:{path}:{System.Guid.NewGuid():N}";
                ResourceTracker.Instance.Register(trackingKey, "GpuResourceCacheItem:Preview", result.Resource, gpuBytes);

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

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            double localMinY = double.MaxValue, localMaxY = double.MinValue;

            foreach (var v in model.Vertices)
            {
                double x = (v.Position.X - model.ModelCenter.X) * model.ModelScale;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                double y = (v.Position.Y - model.ModelCenter.Y) * model.ModelScale;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (y < localMinY) localMinY = y; if (y > localMaxY) localMaxY = y;
                double z = (v.Position.Z - model.ModelCenter.Z) * model.ModelScale;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
            }

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
                    double pMinX = double.MaxValue, pMinY = double.MaxValue, pMinZ = double.MaxValue;
                    double pMaxX = double.MinValue, pMaxY = double.MinValue, pMaxZ = double.MinValue;

                    for (int j = 0; j < part.IndexCount; j++)
                    {
                        int idx = model.Indices[part.IndexOffset + j];
                        var v = model.Vertices[idx];
                        double px = (v.Position.X - model.ModelCenter.X) * model.ModelScale;
                        double py = (v.Position.Y - model.ModelCenter.Y) * model.ModelScale;
                        double pz = (v.Position.Z - model.ModelCenter.Z) * model.ModelScale;

                        if (px < pMinX) pMinX = px; if (px > pMaxX) pMaxX = px;
                        if (py < pMinY) pMinY = py; if (py > pMaxY) pMaxY = py;
                        if (pz < pMinZ) pMinZ = pz; if (pz > pMaxZ) pMaxZ = pz;
                    }

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

        private static void SafeDispose(IDisposable? disposable)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModelManagementService: Dispose failed: {ex.Message}");
            }
        }
    }
}