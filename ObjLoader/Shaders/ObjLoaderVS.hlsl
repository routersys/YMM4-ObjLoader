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
    float4 ToonParams;
    float4 RimParams;
    float4 RimColor;
    float4 OutlineParams;
    float4 OutlineColor;
    float4 FogParams;
    float4 FogColor;
    float4 ColorCorrParams;
    float4 VignetteParams;
    float4 VignetteColor;
    float4 ScanlineParams;
    float4 ChromAbParams;
    float4 MonoParams;
    float4 MonoColor;
    float4 PosterizeParams;
    float4 LightTypeParams;
    matrix LightViewProj;
    float4 ShadowParams;
}

struct VS_IN
{
    float3 pos : POSITION;
    float3 norm : NORMAL;
    float2 uv : TEXCOORD0;
};

struct VS_OUT
{
    float4 pos : SV_POSITION;
    float3 wPos : TEXCOORD1;
    float3 norm : NORMAL;
    float2 uv : TEXCOORD0;
    float4 lightPos : TEXCOORD3;
};

VS_OUT VS(VS_IN input)
{
    VS_OUT output;

    output.pos = mul(float4(input.pos, 1.0f), WorldViewProj);
    output.wPos = mul(float4(input.pos, 1.0f), World).xyz;
    output.norm = normalize(mul(float4(input.norm, 0.0f), World).xyz);
    output.uv = input.uv;
    output.lightPos = mul(float4(output.wPos, 1.0f), LightViewProj);

    return output;
}