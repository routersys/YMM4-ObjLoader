using ObjLoader.Rendering.Core.Buffers;
using ObjLoader.Rendering.Core.Resources;
using ObjLoader.Rendering.Renderers;
using ObjLoader.Services.Rendering.Device;
using System.Numerics;
using Vortice.Direct3D11;

namespace ObjLoader.Services.Rendering.Passes;

internal sealed class RenderPassContext
{
    public ID3D11DeviceContext DeviceContext { get; set; } = null!;
    public D3DResources Resources { get; set; } = null!;
    public List<LayerRenderData> Layers { get; set; } = null!;
    public Matrix4x4[] LayerWorlds { get; set; } = null!;
    public Matrix4x4[] LayerWvps { get; set; } = null!;

    public List<(int LayerIndex, int PartIndex)> OpaqueParts { get; set; } = null!;
    public List<TransparentPart> TransparentParts { get; set; } = null!;

    public ID3D11Buffer GridVertexBuffer { get; set; } = null!;
    public Vector4 GridColor { get; set; }
    public Vector4 AxisColor { get; set; }

    public Matrix4x4 LightViewProj { get; set; } = Matrix4x4.Identity;
    public bool EnableShadow { get; set; }
    public bool RenderShadowMap { get; set; }

    public ID3D11ShaderResourceView[] ShadowSrvArray { get; set; } = null!;
    public ID3D11SamplerState[] SamplerArray { get; set; } = null!;
    public ID3D11SamplerState[] ShadowSamplerArray { get; set; } = null!;
    public IReadOnlyDictionary<string, ID3D11ShaderResourceView> DynamicTextures { get; set; } = null!;

    public Matrix4x4 View { get; set; }
    public Matrix4x4 Proj { get; set; }
    public Vector3 CamPos { get; set; }

    public ConstantBuffer<CBPerFrame> CbPerFrame { get; set; } = null!;
    public ConstantBuffer<CBPerObject> CbPerObject { get; set; } = null!;
    public ConstantBuffer<CBPerMaterial> CbPerMaterialCore { get; set; } = null!;
    public ConstantBuffer<CBSceneEffects> CbSceneEffects { get; set; } = null!;
    public ConstantBuffer<CBPostEffects> CbPostEffects { get; set; } = null!;
    public ID3D11Buffer[] CbPerFrameArray { get; set; } = null!;
    public ID3D11Buffer[] CbPerObjectArray { get; set; } = null!;
    public ID3D11Buffer[] CbPerMaterialArray { get; set; } = null!;
    public ID3D11Buffer[] CbSceneEffectsArray { get; set; } = null!;
    public ID3D11Buffer[] CbPostEffectsArray { get; set; } = null!;

    public bool IsWireframe { get; set; }
    public bool IsInteracting { get; set; }
    public bool IsGridVisible { get; set; }
    public bool IsInfiniteGrid { get; set; }
    public double GridScale { get; set; }

    public ApiObjectRenderer? ApiObjectRenderer { get; set; }
    public LocalDrawManagerAdapter? DrawManagerAdapter { get; set; }

    public ID3D11RenderTargetView MainRtv { get; set; } = null!;
    public ID3D11DepthStencilView MainDsv { get; set; } = null!;
    public int ViewportWidth { get; set; }
    public int ViewportHeight { get; set; }
}