cbuffer CBuf : register(b0)
{
    matrix WorldViewProj;
    matrix World;
    float4 LightPos;
    float4 BaseColor;
    float4 AmbientColor;
    float4 LightColor;
    float4 CameraPos;
    float LightEnabled;
    float DiffuseIntensity;
    float SpecularIntensity;
    float Shininess;
    float4 GridColor;
    float4 GridAxisColor;
}
struct VS_IN
{
    float3 pos : POSITION;
    float3 norm : NORMAL;
    float2 uv : TEXCOORD;
};
struct PS_IN
{
    float4 pos : SV_POSITION;
    float3 wPos : TEXCOORD1;
    float3 norm : NORMAL;
    float2 uv : TEXCOORD0;
};
PS_IN VS(VS_IN input)
{
    PS_IN output;
    output.pos = mul(float4(input.pos, 1.0f), WorldViewProj);
    output.wPos = mul(float4(input.pos, 1.0f), World).xyz;
    output.norm = mul(float4(input.norm, 0.0f), World).xyz;
    output.uv = input.uv;
    return output;
}