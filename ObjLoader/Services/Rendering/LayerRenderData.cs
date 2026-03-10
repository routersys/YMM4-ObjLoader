using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Timeline;
using System.Numerics;
using ObjLoader.Rendering.Mathematics;

internal struct LayerRenderData
{
    public GpuResourceCacheItem Resource;
    public double X, Y, Z;
    public double Scale;
    public double Rx, Ry, Rz;
    public System.Windows.Media.Color BaseColor;
    public bool LightEnabled;
    public int WorldId;
    public double HeightOffset;
    public HashSet<int>? VisibleParts;
    public Matrix4x4? WorldMatrixOverride;
    public int LightType { get; set; }
    public double LightX { get; set; }
    public double LightY { get; set; }
    public double LightZ { get; set; }
    public LayerData? Data { get; set; }
    public Vortice.Direct3D11.ID3D11Buffer? OverrideVB;
    public CullingBox WorldBoundingBox;
    public bool IsAnimated;
}