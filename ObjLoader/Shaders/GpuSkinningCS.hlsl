struct Vertex
{
    float3 Position;
    float3 Normal;
    float2 TexCoord;
    float4 Color;
};
struct BoneWeight
{
    int4 Indices;
    float4 Weights;
};
cbuffer Constants : register(b0)
{
    uint vertexCount;
    uint boneCount;
    uint pad0;
    uint pad1;
};
StructuredBuffer<Vertex> InputVertices : register(t0);
StructuredBuffer<BoneWeight> Weights : register(t1);
StructuredBuffer<float4x4> BoneMatrices : register(t2);
RWStructuredBuffer<Vertex> OutputVertices : register(u0);

[numthreads(256, 1, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= vertexCount)
        return;

    Vertex src = InputVertices[id.x];
    BoneWeight bw = Weights[id.x];

    float3 skinnedPos = float3(0, 0, 0);
    float3 skinnedNormal = float3(0, 0, 0);

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        int idx = 0;
        float w = 0;
        if (i == 0)
        {
            idx = bw.Indices.x;
            w = bw.Weights.x;
        }
        else if (i == 1)
        {
            idx = bw.Indices.y;
            w = bw.Weights.y;
        }
        else if (i == 2)
        {
            idx = bw.Indices.z;
            w = bw.Weights.z;
        }
        else
        {
            idx = bw.Indices.w;
            w = bw.Weights.w;
        }

        if (idx >= 0 && idx < (int) boneCount && w > 0)
        {
            float4x4 m = BoneMatrices[idx];
            skinnedPos += mul(m, float4(src.Position, 1)).xyz * w;
            skinnedNormal += mul(m, float4(src.Normal, 0)).xyz * w;
        }
    }

    float nLen = length(skinnedNormal);
    if (nLen > 0)
        skinnedNormal /= nLen;

    Vertex result;
    result.Position = skinnedPos;
    result.Normal = skinnedNormal;
    result.TexCoord = src.TexCoord;
    result.Color = src.Color;
    OutputVertices[id.x] = result;
}