using ObjLoader.Cache.Gpu;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Core.Resources;
using ObjLoader.Services.Textures;
using ObjLoader.Settings;
using ObjLoader.Utilities.Logging;
using System.IO;
using Vortice.Direct3D11;
using Vector3 = System.Numerics.Vector3;

namespace ObjLoader.Services.Rendering.Scene;

internal sealed class ModelLoaderService : IDisposable
{
    private const string CacheKeyPrefix = "scene:";

    private readonly ObjLoaderParameter _parameter;
    private readonly RenderService _renderService;
    private ID3D11Device? _device => _renderService.Device;
    private readonly ObjModelLoader _loader;
    private readonly ITextureService _textureService;
    private readonly GpuResourceFactory _resourceFactory;

    private readonly Dictionary<string, (Vector3 Size, Vector3 Min, Vector3 Max)> _boundingData = new();
    private readonly HashSet<string> _managedCacheKeys = new();

    private bool _isLoadingModel;
    private bool _isModelLoaded;
    private Task? _loadModelTask;
    private readonly object _loadLock = new();

    public Action? OnModelLoaded { get; set; }
    public double ModelScale { get; private set; } = 1.0;
    public double ModelHeight { get; private set; } = 1.0;

    public ModelLoaderService(ObjLoaderParameter parameter, RenderService renderService)
    {
        _parameter = parameter;
        _renderService = renderService;
        _loader = new ObjModelLoader();
        _textureService = new TextureService();
        _resourceFactory = new GpuResourceFactory(() => _device, _textureService, CacheKeyPrefix);
    }

    private static string ToCacheKey(string path) => $"{CacheKeyPrefix}{path}";

    public bool IsModelLoaded => _isModelLoaded;

    public IReadOnlyDictionary<string, (Vector3 Size, Vector3 Min, Vector3 Max)> BoundingData => _boundingData;

    public void LoadModel()
    {
        lock (_loadLock)
        {
            if (_isLoadingModel) return;
            _isLoadingModel = true;
        }

        try
        {
            LoadModelInternal();
        }
        finally
        {
            lock (_loadLock)
            {
                _isLoadingModel = false;
            }
        }
    }

    public async Task LoadModelAsync()
    {
        lock (_loadLock)
        {
            if (_isLoadingModel || _isModelLoaded) return;
            _isLoadingModel = true;
        }

        try
        {
            await Task.Run(LoadModelInternal).ConfigureAwait(false);
            _isModelLoaded = true;
        }
        catch (Exception ex)
        {
            Logger<ModelLoaderService>.Instance.Error("Failed to load model asynchronously", ex);
        }
        finally
        {
            lock (_loadLock)
            {
                _isLoadingModel = false;
            }
            OnModelLoaded?.Invoke();
        }
    }

    public void EnsureModelLoadedAsyncIfNeeded()
    {
        if (!_isModelLoaded)
        {
            lock (_loadLock)
            {
                if (_loadModelTask == null || _loadModelTask.IsCompleted)
                {
                    _loadModelTask = LoadModelAsync();
                }
            }
        }
    }

    public void ComputeModelScale()
    {
        var validPaths = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(_parameter.FilePath))
            validPaths.Add(_parameter.FilePath.Trim('"'));
        foreach (var layer in _parameter.Layers)
        {
            if (!string.IsNullOrWhiteSpace(layer.FilePath))
                validPaths.Add(layer.FilePath);
        }

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        bool hasData = false;

        foreach (var path in validPaths)
        {
            if (!File.Exists(path)) continue;

            try
            {
                var model = _loader.Load(path);
                if (model.Vertices.Length == 0) continue;

                foreach (var v in model.Vertices)
                {
                    double x = (v.Position.X - model.ModelCenter.X) * model.ModelScale;
                    double y = (v.Position.Y - model.ModelCenter.Y) * model.ModelScale;
                    double z = (v.Position.Z - model.ModelCenter.Z) * model.ModelScale;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                    if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                }
                hasData = true;
            }
            catch (IOException ex)
            {
                Logger<ModelLoaderService>.Instance.Warning($"IO error loading model for scale computation: {path}", ex);
            }
            catch (InvalidDataException ex)
            {
                Logger<ModelLoaderService>.Instance.Warning($"Invalid model data for scale computation: {path}", ex);
            }
        }

        if (hasData)
        {
            ModelScale = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
            ModelHeight = maxY - minY;
            if (ModelScale < 0.1) ModelScale = 1.0;
        }
    }

    private unsafe void LoadModelInternal()
    {
        var validPaths = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(_parameter.FilePath))
            validPaths.Add(_parameter.FilePath.Trim('"'));

        foreach (var layer in _parameter.Layers)
        {
            if (!string.IsNullOrWhiteSpace(layer.FilePath))
                validPaths.Add(layer.FilePath);
        }

        var keysToRemove = new List<string>();
        foreach (var key in _boundingData.Keys)
        {
            if (!validPaths.Contains(key))
                keysToRemove.Add(key);
        }

        foreach (var key in keysToRemove)
        {
            string cacheKey = ToCacheKey(key);
            GpuResourceCache.Instance.Remove(cacheKey);
            _managedCacheKeys.Remove(cacheKey);
            _boundingData.Remove(key);
        }

        var modelSettings = ModelSettings.Instance;

        foreach (var path in validPaths)
        {
            if (_boundingData.ContainsKey(path))
            {
                string cacheKey = ToCacheKey(path);
                if (GpuResourceCache.Instance.TryGetValue(cacheKey, out var existing) && existing.Device == _device)
                {
                    continue;
                }
            }

            if (!File.Exists(path)) continue;

            var model = _loader.Load(path);
            if (model.Vertices.Length == 0) continue;

            if (_device != null)
            {
                var cachedItem = _resourceFactory.Create(model, path);
                if (cachedItem != null)
                {
                    _managedCacheKeys.Add(ToCacheKey(path));
                }
            }

            double localMinX = double.MaxValue, localMaxX = double.MinValue;
            double localMinY = double.MaxValue, localMaxY = double.MinValue;
            double localMinZ = double.MaxValue, localMaxZ = double.MinValue;

            foreach (var v in model.Vertices)
            {
                double x = (v.Position.X - model.ModelCenter.X) * model.ModelScale;
                if (x < localMinX) localMinX = x; if (x > localMaxX) localMaxX = x;

                double y = (v.Position.Y - model.ModelCenter.Y) * model.ModelScale;
                if (y < localMinY) localMinY = y; if (y > localMaxY) localMaxY = y;

                double z = (v.Position.Z - model.ModelCenter.Z) * model.ModelScale;
                if (z < localMinZ) localMinZ = z; if (z > localMaxZ) localMaxZ = z;
            }

            Vector3 size = new Vector3((float)(localMaxX - localMinX), (float)(localMaxY - localMinY), (float)(localMaxZ - localMinZ));
            Vector3 min = new Vector3((float)localMinX, (float)localMinY, (float)localMinZ);
            Vector3 max = new Vector3((float)localMaxX, (float)localMaxY, (float)localMaxZ);

            _boundingData[path] = (size, min, max);
        }

        if (_boundingData.Count > 0)
        {
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (var entry in _boundingData.Values)
            {
                if (entry.Min.X < minX) minX = entry.Min.X;
                if (entry.Min.Y < minY) minY = entry.Min.Y;
                if (entry.Min.Z < minZ) minZ = entry.Min.Z;

                if (entry.Max.X > maxX) maxX = entry.Max.X;
                if (entry.Max.Y > maxY) maxY = entry.Max.Y;
                if (entry.Max.Z > maxZ) maxZ = entry.Max.Z;
            }

            ModelScale = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
            ModelHeight = maxY - minY;
            if (ModelScale < 0.1) ModelScale = 1.0;
        }
    }

    public GpuResourceCacheItem? GetCachedResource(string path)
    {
        string cacheKey = ToCacheKey(path);
        if (GpuResourceCache.Instance.TryGetValue(cacheKey, out var cached) && cached.Device == _device)
        {
            return cached;
        }
        return null;
    }

    public void Dispose()
    {
        foreach (var cacheKey in _managedCacheKeys)
        {
            GpuResourceCache.Instance.Remove(cacheKey);
        }
        _managedCacheKeys.Clear();
        _boundingData.Clear();

        if (_textureService is IDisposable disposableTextureService)
        {
            disposableTextureService.Dispose();
        }
    }
}