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
Texture2D tex : register(t0);
SamplerState sam : register(s0);
struct PS_IN
{
    float4 pos : SV_POSITION;
    float3 wPos : TEXCOORD1;
    float3 norm : NORMAL;
    float2 uv : TEXCOORD0;
};
float4 PS(PS_IN input) : SV_Target
{
    float4 texColor = tex.Sample(sam, input.uv) * BaseColor;
    
    if (LightEnabled <= 0.5f)
    {
        return texColor;
    }

    float3 ambient = texColor.rgb * (AmbientColor.rgb + 0.3f);
    float3 n = normalize(input.norm);
    float3 lightDir = normalize(LightPos.xyz - input.wPos);
    float diff = dot(n, lightDir) * 0.5f + 0.5f;
    float3 diffuse = texColor.rgb * LightColor.rgb * diff * DiffuseIntensity;
    float3 viewDir = normalize(CameraPos.xyz - input.wPos);
    float3 halfDir = normalize(lightDir + viewDir);
    float spec = pow(max(dot(n, halfDir), 0.0f), Shininess);
    float3 specular = LightColor.rgb * spec * SpecularIntensity;
    return float4(ambient + diffuse + specular, texColor.a);
}