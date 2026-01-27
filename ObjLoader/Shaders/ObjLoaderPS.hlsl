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
    matrix LightViewProj0;
    matrix LightViewProj1;
    matrix LightViewProj2;
    float4 ShadowParams;
    float4 CascadeSplits;
}

Texture2D tex : register(t0);
SamplerState sam : register(s0);
Texture2DArray ShadowMap : register(t1);
SamplerComparisonState ShadowSampler : register(s1);

struct PS_IN
{
    float4 pos : SV_POSITION;
    float3 wPos : TEXCOORD1;
    float3 norm : NORMAL;
    float2 uv : TEXCOORD0;
};

float3 RGBtoHSV(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 HSVtoRGB(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

float CalculateShadow(float3 wPos, float bias)
{
    float dist = distance(wPos, CameraPos.xyz);
    int cascadeIndex = 0;
    matrix lightVP = LightViewProj0;
    
    if (dist > CascadeSplits.x)
    {
        cascadeIndex = 1;
        lightVP = LightViewProj1;
    }
    if (dist > CascadeSplits.y)
    {
        cascadeIndex = 2;
        lightVP = LightViewProj2;
    }
    
    float4 lpos = mul(float4(wPos, 1.0f), lightVP);
    float3 projCoords = lpos.xyz / lpos.w;
    projCoords.x = projCoords.x * 0.5 + 0.5;
    projCoords.y = -projCoords.y * 0.5 + 0.5;

    if (projCoords.z > 1.0 || projCoords.x < 0.0 || projCoords.x > 1.0 || projCoords.y < 0.0 || projCoords.y > 1.0)
    {
        return 1.0;
    }

    float currentDepth = projCoords.z;
    float shadow = 0.0;
    float2 texelSize = 1.0 / ShadowParams.w;

    [unroll]
    for (int x = -1; x <= 1; ++x)
    {
        [unroll]
        for (int y = -1; y <= 1; ++y)
        {
            shadow += ShadowMap.SampleCmpLevelZero(ShadowSampler, float3(projCoords.xy + float2(x, y) * texelSize, cascadeIndex), currentDepth - bias);
        }
    }
    
    return shadow / 9.0;
}

float4 PS(PS_IN input) : SV_Target
{
    float2 uv = input.uv;
    float4 texColor;
    if (ChromAbParams.x > 0.5f)
    {
        float2 dist = uv - 0.5f;
        float2 offset = dist * ChromAbParams.y;
        float r = tex.Sample(sam, uv - offset).r;
        float g = tex.Sample(sam, uv).g;
        float b = tex.Sample(sam, uv + offset).b;
        float4 original = tex.Sample(sam, uv);
        texColor = float4(r, g, b, original.a) * BaseColor;
    }
    else
    {
        texColor = tex.Sample(sam, uv) * BaseColor;
    }

    float3 finalColor;
    float3 n = normalize(input.norm);
    float3 viewDir = normalize(CameraPos.xyz - input.wPos);
    
    if (LightEnabled > 0.5f)
    {
        float3 lightDir;
        float attenuation = 1.0f;
        int type = (int) LightTypeParams.x;

        if (type == 2)
        {
            lightDir = normalize(LightPos.xyz);
        }
        else
        {
            lightDir = normalize(LightPos.xyz - input.wPos);
        }

        if (type != 2)
        {
            float dist = distance(LightPos.xyz, input.wPos);
            attenuation = 1.0 / (1.0 + 0.0001 * dist * dist);
        }

        if (type == 1)
        {
            float3 spotDir = normalize(-LightPos.xyz);
            float cosAngle = dot(-lightDir, spotDir);
            attenuation *= smoothstep(0.86, 0.90, cosAngle);
        }

        float NdotL = dot(n, lightDir);
        float diff = NdotL * 0.5f + 0.5f;

        if (type == 3)
        {
            diff = pow(diff, 0.5);
        }

        if (ToonParams.x > 0.5f)
        {
            float steps = ToonParams.y;
            float smooth = ToonParams.z;
            diff = smoothstep(0, smooth, diff * steps - floor(diff * steps)) / steps + floor(diff * steps) / steps;
        }

        float shadow = 1.0f;
        if (ShadowParams.x > 0.5f && (type == 1 || type == 2))
        {
            float bias = max(ShadowParams.y * (1.0 - NdotL), ShadowParams.y * 0.1);
            float sVal = CalculateShadow(input.wPos, bias);
            shadow = lerp(1.0 - ShadowParams.z, 1.0, sVal);
        }

        float3 ambient = texColor.rgb * (AmbientColor.rgb + 0.3f);
        float3 diffuse = texColor.rgb * LightColor.rgb * diff * DiffuseIntensity * attenuation * shadow;

        float3 halfDir = normalize(lightDir + viewDir);
        float NdotH = max(dot(n, halfDir), 0.0f);
        float spec = pow(abs(NdotH), Shininess);
        if (ToonParams.x > 0.5f)
        {
            float specSmooth = 0.01f;
            spec = smoothstep(0.5 - specSmooth, 0.5 + specSmooth, spec);
        }

        float3 specular = LightColor.rgb * spec * SpecularIntensity * attenuation * shadow;
        finalColor = ambient + diffuse + specular;
        
        if (RimParams.x > 0.5f)
        {
            float vdn = 1.0 - max(dot(viewDir, n), 0.0);
            float rim = pow(abs(smoothstep(0.0, 1.0, vdn)), RimParams.z) * RimParams.y;
            finalColor += RimColor.rgb * rim;
        }
    }
    else
    {
        finalColor = texColor.rgb;
    }
    
    if (OutlineParams.x > 0.5f)
    {
        float vdn = max(dot(viewDir, n), 0.0);
        float threshold = 1.0 / (OutlineParams.y * 5.0 + 1.0);
        if (vdn < threshold)
        {
            float edgeFactor = smoothstep(threshold - 0.05, threshold, vdn);
            finalColor = lerp(OutlineColor.rgb, finalColor, edgeFactor);
        }
    }
    
    if (FogParams.x > 0.5f)
    {
        float dist = distance(CameraPos.xyz, input.wPos);
        float fogFactor = saturate((dist - FogParams.y) / (FogParams.z - FogParams.y));
        finalColor = lerp(finalColor, FogColor.rgb, fogFactor * FogParams.w);
    }
    
    if (ScanlineParams.x > 0.5f)
    {
        float scanline = sin(uv.y * ScanlineParams.z * 3.14159) * 0.5 + 0.5;
        finalColor *= 1.0 - (scanline * ScanlineParams.y);
    }
    
    if (MonoParams.x > 0.5f)
    {
        float lum = dot(finalColor, float3(0.299, 0.587, 0.114));
        float3 mono = lerp(float3(lum, lum, lum), MonoColor.rgb * lum, 0.5);
        finalColor = lerp(finalColor, mono, MonoParams.y);
    }
    
    if (PosterizeParams.x > 0.5f)
    {
        float levels = PosterizeParams.y;
        finalColor = floor(finalColor * levels) / levels;
    }
    
    float3 hsv = RGBtoHSV(finalColor);
    hsv.y *= ColorCorrParams.x;
    finalColor = HSVtoRGB(hsv);
    
    finalColor = (finalColor - 0.5f) * ColorCorrParams.y + 0.5f;
    finalColor = pow(abs(finalColor), 1.0f / ColorCorrParams.z);
    finalColor += ColorCorrParams.w;
    
    if (VignetteParams.x > 0.5f)
    {
        float2 d = uv - 0.5f;
        float v = length(d);
        float vig = smoothstep(VignetteParams.z, VignetteParams.z - VignetteParams.w, v);
        finalColor = lerp(VignetteColor.rgb, finalColor, vig + (1.0 - VignetteParams.y));
    }

    return float4(saturate(finalColor), texColor.a);
}