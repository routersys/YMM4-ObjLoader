using System.IO;
using System.Runtime.CompilerServices;
using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Models;
using ObjLoader.Localization;
using ObjLoader.Services.Textures;
using ObjLoader.Settings;
using ObjLoader.Utilities;
using Vortice.Direct3D11;
using ObjLoader.Rendering.Mathematics;


namespace ObjLoader.Rendering.Core.Resources;

internal sealed class GpuResourceFactory(Func<ID3D11Device?> deviceProvider, ITextureService textureService, string cacheKeyPrefix)
{
    private string GetCacheKey(string filePath) => $"{cacheKeyPrefix}{filePath}";

    public unsafe GpuResourceCacheItem? Create(ObjModel model, string filePath)
    {
        var device = deviceProvider();
        if (device == null) return null;

        ID3D11Buffer? vb = null;
        ID3D11Buffer? ib = null;
        ID3D11ShaderResourceView?[]? partTextures = null;
        bool success = false;
        long gpuBytes = 0;

        try
        {
            int vertexBufferSize = model.Vertices.Length * Unsafe.SizeOf<ObjVertex>();
            var vDesc = new BufferDescription(
                vertexBufferSize,
                BindFlags.VertexBuffer,
                ResourceUsage.Immutable,
                CpuAccessFlags.None);

            fixed (ObjVertex* pVerts = model.Vertices)
            {
                var vData = new SubresourceData(pVerts);
                vb = device.CreateBuffer(vDesc, vData);
            }
            gpuBytes += vertexBufferSize;

            int indexBufferSize = model.Indices.Length * sizeof(int);
            var iDesc = new BufferDescription(
                indexBufferSize,
                BindFlags.IndexBuffer,
                ResourceUsage.Immutable,
                CpuAccessFlags.None);

            fixed (int* pIndices = model.Indices)
            {
                var iData = new SubresourceData(pIndices);
                ib = device.CreateBuffer(iDesc, iData);
            }
            gpuBytes += indexBufferSize;

            var (globalBox, parts) = BoundingBoxUtility.CalculateBounds(model);
            model.LocalBoundingBox = globalBox;
            model.Parts = parts.ToList();
            partTextures = new ID3D11ShaderResourceView?[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                string tPath = parts[i].TexturePath;
                if (string.IsNullOrEmpty(tPath) || !File.Exists(tPath)) continue;

                try
                {
                    var (srv, texGpuBytes) = textureService.CreateShaderResourceView(tPath, device);
                    partTextures[i] = srv;
                    gpuBytes += texGpuBytes;
                }
                catch (Exception)
                {
                }
            }

            var modelSettings = ModelSettings.Instance;
            if (!modelSettings.IsGpuMemoryPerModelAllowed(gpuBytes))
            {
                long gpuMB = gpuBytes / (1024L * 1024L);
                string message = string.Format(
                    Texts.GpuMemoryExceeded,
                    Path.GetFileName(filePath),
                    gpuMB,
                    modelSettings.MaxGpuMemoryPerModelMB);
                UserNotification.ShowWarning(message, Texts.ResourceLimitTitle);
                return null;
            }

            var item = new GpuResourceCacheItem(device, vb, ib, model.Indices.Length, parts, partTextures, model.ModelCenter, model.ModelScale, globalBox, gpuBytes);
            string cacheKey = GetCacheKey(filePath);
            GpuResourceCache.Instance.AddOrUpdate(cacheKey, item);
            success = true;
            return item;
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
    }

    private static void SafeDispose(IDisposable? disposable)
    {
        try
        {
            disposable?.Dispose();
        }
        catch (Exception)
        {
        }
    }
}