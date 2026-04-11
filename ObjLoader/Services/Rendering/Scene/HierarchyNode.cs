using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Timeline;
using System.Numerics;

namespace ObjLoader.Services.Rendering.Scene;

internal readonly struct HierarchyNode
{
    public readonly Matrix4x4 Local;
    public readonly string? ParentId;
    public readonly LayerData Layer;
    public readonly GpuResourceCacheItem Resource;

    public HierarchyNode(Matrix4x4 local, string? parentId, LayerData layer, GpuResourceCacheItem resource)
    {
        Local = local;
        ParentId = parentId;
        Layer = layer;
        Resource = resource;
    }
}