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
    float4 EnvironmentParam;
    float4 PbrParams;
    float4 IblParams;
    float4 SsrParams;
    matrix InverseViewProj;
    matrix ViewProj;
    float4 PcssParams;
    float4 SsrParams2;
}

Texture2D tex : register(t0);
SamplerState sam : register(s0);
Texture2DArray ShadowMap : register(t1);
SamplerComparisonState ShadowSampler : register(s1);
TextureCube EnvironmentMap : register(t2);
Texture2D DepthMap : register(t3);

struct PS_IN
{
    float4 pos : SV_POSITION;
    float3 wPos : TEXCOORD1;
    float3 norm : NORMAL;
    float2 uv : TEXCOORD0;
    float4 screenPos : TEXCOORD3;
};

static const float PI = 3.14159265359;

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

float GetShadowBias(float3 N, float3 L)
{
    float NdotL = max(dot(N, L), 0.0);
    return max(ShadowParams.y * (1.0 - NdotL), ShadowParams.y * 0.1);
}

float2 VogelDiskSample(int sampleIndex, int samplesCount, float phi)
{
    float GoldenAngle = 2.4;
    float r = sqrt(float(sampleIndex) + 0.5) / sqrt(float(samplesCount));
    float theta = float(sampleIndex) * GoldenAngle + phi;
    float2 sine_cosine;
    sincos(theta, sine_cosine.x, sine_cosine.y);
    return r * sine_cosine;
}

float FindBlocker(float3 projCoords, float bias, float lightSize, int cascadeIndex, float randomRotation)
{
    float2 texelSize = 1.0 / ShadowParams.w;
    float blockerSum = 0.0;
    float numBlockers = 0.0;
    float searchWidth = lightSize * PcssParams.y;
    int samples = min((int) round(PcssParams.z), 64);
    
    for (int i = 0; i < samples; ++i)
    {
        float2 offset = VogelDiskSample(i, samples, randomRotation) * searchWidth * texelSize;
        float shadowMapDepth = ShadowMap.SampleLevel(sam, float3(projCoords.xy + offset, cascadeIndex), 0).r;
        
        if (shadowMapDepth < (projCoords.z - bias))
        {
            blockerSum += shadowMapDepth;
            numBlockers += 1.0;
        }
    }
    
    if (numBlockers < 1.0)
        return -1.0;
    return blockerSum / numBlockers;
}

float CalculatePCSS(float3 wPos, float3 N, float3 L)
{
    float dist = distance(wPos, CameraPos.xyz);
    int cascadeIndex = 0;
    matrix lightVP = LightViewProj0;
    float blendFactor = 0.0;
    
    if (dist > CascadeSplits.x)
    {
        cascadeIndex = 1;
        lightVP = LightViewProj1;
        blendFactor = saturate((dist - CascadeSplits.x) / (CascadeSplits.x * 0.1));
    }
    if (dist > CascadeSplits.y)
    {
        cascadeIndex = 2;
        lightVP = LightViewProj2;
        blendFactor = saturate((dist - CascadeSplits.y) / (CascadeSplits.y * 0.1));
    }
    
    float4 lpos = mul(float4(wPos, 1.0f), lightVP);
    float3 projCoords = lpos.xyz / lpos.w;
    projCoords.x = projCoords.x * 0.5 + 0.5;
    projCoords.y = -projCoords.y * 0.5 + 0.5;
    
    if (projCoords.z > 1.0 || projCoords.x < 0.0 || projCoords.x > 1.0 || projCoords.y < 0.0 || projCoords.y > 1.0)
        return 1.0;
        
    float bias = GetShadowBias(N, L);
    float currentDepth = projCoords.z;
    float randomRotation = frac(sin(dot(wPos.xy, float2(12.9898, 78.233))) * 43758.5453) * 6.28318;

    float shadow = 0.0;
    
    if (PcssParams.x <= 0.0)
    {
        float2 texelSize = 1.0 / ShadowParams.w;
        int samples = 16;
        
        for (int i = 0; i < samples; ++i)
        {
            float2 offset = VogelDiskSample(i, samples, randomRotation) * texelSize * 2.0;
            shadow += ShadowMap.SampleCmpLevelZero(ShadowSampler, float3(projCoords.xy + offset, cascadeIndex), currentDepth - bias);
        }
        shadow /= float(samples);
    }
    else
    {
        float avgBlockerDepth = FindBlocker(projCoords, bias, PcssParams.x, cascadeIndex, randomRotation);
        
        if (avgBlockerDepth < 0.0)
            return 1.0;
            
        float penumbraRatio = (currentDepth - avgBlockerDepth) / avgBlockerDepth;
        float filterRadius = penumbraRatio * PcssParams.x * PcssParams.y;
        float2 texelSize = 1.0 / ShadowParams.w;
        
        int samples = min((int) round(PcssParams.w), 64);
        for (int i = 0; i < samples; ++i)
        {
            float2 offset = VogelDiskSample(i, samples, randomRotation) * filterRadius * texelSize;
            shadow += ShadowMap.SampleCmpLevelZero(ShadowSampler, float3(projCoords.xy + offset, cascadeIndex), currentDepth - bias);
        }
        shadow /= float(samples);
    }
    
    if (cascadeIndex > 0 && blendFactor > 0.0)
    {
        int prevCascade = cascadeIndex - 1;
        matrix prevLightVP = (cascadeIndex == 1) ? LightViewProj0 : LightViewProj1;
        
        float4 prevLpos = mul(float4(wPos, 1.0f), prevLightVP);
        float3 prevProjCoords = prevLpos.xyz / prevLpos.w;
        prevProjCoords.x = prevProjCoords.x * 0.5 + 0.5;
        prevProjCoords.y = -prevProjCoords.y * 0.5 + 0.5;
        
        if (prevProjCoords.z <= 1.0 && prevProjCoords.x >= 0.0 && prevProjCoords.x <= 1.0 &&
            prevProjCoords.y >= 0.0 && prevProjCoords.y <= 1.0)
        {
            float prevShadow = 0.0;
            float2 texelSize = 1.0 / ShadowParams.w;
            int samples = 16;
            
            for (int i = 0; i < samples; ++i)
            {
                float2 offset = VogelDiskSample(i, samples, randomRotation) * texelSize * 2.0;
                prevShadow += ShadowMap.SampleCmpLevelZero(ShadowSampler, float3(prevProjCoords.xy + offset, prevCascade),
                    prevProjCoords.z - bias);
            }
            prevShadow /= float(samples);
            
            shadow = lerp(prevShadow, shadow, blendFactor);
        }
    }
    
    return shadow;
}

float DistributionGGX(float3 N, float3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;
    return num / max(denom, 0.0001);
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;
    float num = NdotV;
    float denom = NdotV * (1.0 - k) + k;
    return num / max(denom, 0.0001);
}

float GeometrySmith(float3 N, float3 V, float3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);
    return ggx1 * ggx2;
}

float3 FresnelSchlick(float cosTheta, float3 F0)
{
    return F0 + (1.0 - F0) * pow(saturate(1.0 - cosTheta), 5.0);
}

float3 FresnelSchlickRoughness(float cosTheta, float3 F0, float roughness)
{
    return F0 + (max(float3(1.0 - roughness, 1.0 - roughness, 1.0 - roughness), F0) - F0) *
        pow(saturate(1.0 - cosTheta), 5.0);
}

float2 IntegrateBRDF(float NdotV, float roughness)
{
    const float4 c0 = float4(-1, -0.0275, -0.572, 0.022);
    const float4 c1 = float4(1, 0.0425, 1.04, -0.04);
    float4 r = roughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * NdotV)) * r.x + r.y;
    float2 AB = float2(-1.04, 1.04) * a004 + r.zw;
    
    float smoothness = 1.0 - roughness;
    float energyBoost = smoothness * smoothness * 0.15;
    AB.x = min(AB.x * (1.0 + energyBoost), 1.0);
    AB.y = max(AB.y, 0.0);
    
    return AB;
}

float3 CalculateSSR(float3 wPos, float3 N, float3 V, float roughness, out float hit)
{
    hit = 0.0;
    if (SsrParams.x < 0.5f)
        return float3(0, 0, 0);

    float3 R = normalize(reflect(-V, N));
    int maxSteps = (int) SsrParams2.x;
    float maxDist = SsrParams.z;
    float depthThreshold = SsrParams2.y;
    
    float3 currentPos = wPos;
    float3 ssrColor = float3(0, 0, 0);
    float stepSize = SsrParams.y;
    
    [loop]
    for (int i = 0; i < maxSteps; i++)
    {
        float adaptiveStep = stepSize * (1.0 + float(i) * 0.05);
        currentPos += R * adaptiveStep;
        
        float dist = distance(wPos, currentPos);
        if (dist > maxDist)
            break;
            
        float4 clipPos = mul(float4(currentPos, 1.0f), ViewProj);
        if (clipPos.w <= 0.0)
            continue;
            
        float2 screenUV = (clipPos.xy / clipPos.w) * float2(0.5, -0.5) + 0.5;
        
        if (screenUV.x < 0.0 || screenUV.x > 1.0 || screenUV.y < 0.0 || screenUV.y > 1.0)
            break;
            
        float depthSample = DepthMap.SampleLevel(sam, screenUV, 0).r;
        float currentDepth = clipPos.z / clipPos.w;
        float depthDiff = currentDepth - depthSample;
        
        if (depthDiff > 0.0 && depthDiff < depthThreshold)
        {
            float3 start = currentPos - R * adaptiveStep;
            float3 end = currentPos;
            
            for (int j = 0; j < 8; j++)
            {
                float3 mid = (start + end) * 0.5;
                float4 midClip = mul(float4(mid, 1.0f), ViewProj);
                float2 midUV = (midClip.xy / midClip.w) * float2(0.5, -0.5) + 0.5;
                float midDepthSample = DepthMap.SampleLevel(sam, midUV, 0).r;
                float midCurrentDepth = midClip.z / midClip.w;
                
                if (midCurrentDepth > midDepthSample)
                    end = mid;
                else
                    start = mid;
            }
            
            currentPos = start;
            float4 finalClip = mul(float4(currentPos, 1.0f), ViewProj);
            float2 finalUV = (finalClip.xy / finalClip.w) * float2(0.5, -0.5) + 0.5;
            
            float edgeFade = 1.0 - pow(saturate(max(abs(finalUV.x - 0.5), abs(finalUV.y - 0.5)) * 2.0), 2.0);
            float distFade = 1.0 - saturate(dist / maxDist);
            float roughnessFade = 1.0 - saturate(roughness * 2.0);
            float fade = edgeFade * distFade * roughnessFade;
            
            hit = fade;
            ssrColor = tex.SampleLevel(sam, finalUV, roughness * 8.0).rgb * fade;
            break;
        }
    }
    
    return ssrColor;
}

float3 AceSToneMapping(float3 color)
{
    float a = 2.51f;
    float b = 0.03f;
    float c = 2.43f;
    float d = 0.59f;
    float e = 0.14f;
    return saturate((color * (a * color + b)) / (color * (c * color + d) + e));
}

float3 ApplyScanline(float3 color, float2 uv)
{
    float scanline = sin(uv.y * ScanlineParams.z * 3.14159) * 0.5 + 0.5;
    return color * (1.0 - (scanline * ScanlineParams.y));
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

    float3 albedo = pow(abs(texColor.rgb), 2.2);
    float metallic = saturate(PbrParams.x);
    float roughness = saturate(PbrParams.y);
    float ao = saturate(PbrParams.z);
    
    float3 N = normalize(input.norm);
    float3 V = normalize(CameraPos.xyz - input.wPos);
    float3 F0 = float3(0.04, 0.04, 0.04);
    F0 = lerp(F0, albedo, metallic);

    float3 Lo = float3(0.0, 0.0, 0.0);
    
    if (LightEnabled > 0.5f)
    {
        float3 lightDir;
        float attenuation = 1.0f;
        int type = (int) LightTypeParams.x;

        if (type == 2)
            lightDir = normalize(LightPos.xyz);
        else
            lightDir = normalize(LightPos.xyz - input.wPos);
            
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

        float3 L = lightDir;
        float3 H = normalize(V + L);
        float NdotL = max(dot(N, L), 0.0);
        
        float shadow = 1.0f;
        if (ShadowParams.x > 0.5f && (type == 1 || type == 2))
        {
            float sVal = CalculatePCSS(input.wPos, N, L);
            shadow = lerp(1.0 - ShadowParams.z, 1.0, sVal);
        }

        float3 radiance = LightColor.rgb * attenuation * shadow * DiffuseIntensity;
        float NDF = DistributionGGX(N, H, roughness);
        float G = GeometrySmith(N, V, L, roughness);
        float3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);
        
        float3 numerator = NDF * G * F;
        float denominator = 4.0 * max(dot(N, V), 0.0001) * max(NdotL, 0.0001);
        float3 specular = numerator / denominator * SpecularIntensity;

        float3 kS = F;
        float3 kD = float3(1.0, 1.0, 1.0) - kS;
        kD *= 1.0 - metallic;
        kD = max(kD, 0.0);

        Lo += (kD * albedo / PI + specular) * radiance * NdotL;
    }

    float3 ambient = float3(0, 0, 0);
    
    if (EnvironmentParam.x > 0.5f && LightEnabled > 0.5f)
    {
        float NdotV = max(dot(N, V), 0.0001);
        float3 kS = FresnelSchlickRoughness(NdotV, F0, roughness);
        float3 kD = saturate(1.0 - kS);
        kD *= 1.0 - metallic;
        
        float maxMip = IblParams.y > 0.0 ? IblParams.y : 6.0;
        float3 irradiance = EnvironmentMap.SampleLevel(sam, N, maxMip).rgb;
        float3 diffuse = irradiance * albedo;
        
        float3 R = reflect(-V, N);
        float3 prefilteredColor = EnvironmentMap.SampleLevel(sam, R, roughness * maxMip).rgb;
        float2 envBRDF = IntegrateBRDF(NdotV, roughness);
        float3 specularIBL = prefilteredColor * (kS * envBRDF.x + envBRDF.y);
        
        float hit = 0.0;
        float3 ssr = float3(0, 0, 0);
        
        if (SsrParams.x > 0.5f)
        {
            ssr = CalculateSSR(input.wPos, N, V, roughness, hit);
        }
        
        float3 finalSpecular = lerp(specularIBL, ssr, hit);
        ambient = (kD * diffuse + finalSpecular) * ao * IblParams.x;
    }
    else
    {
        if (LightEnabled > 0.5f)
        {
            ambient = float3(0.03, 0.03, 0.03) * albedo * ao;
        }
        else
        {
            ambient = albedo;
        }
    }

    float3 color = ambient + Lo;
    
    if (RimParams.x > 0.5f)
    {
        float vdn = 1.0 - max(dot(V, N), 0.0);
        float rim = pow(saturate(vdn), RimParams.z) * RimParams.y;
        color += RimColor.rgb * rim;
    }

    if (OutlineParams.x > 0.5f)
    {
        float vdn = max(dot(V, N), 0.0);
        float threshold = 1.0 / (OutlineParams.y * 5.0 + 1.0);
        if (vdn < threshold)
        {
            float edgeFactor = smoothstep(threshold - 0.05, threshold, vdn);
            color = lerp(OutlineColor.rgb, color, edgeFactor);
        }
    }

    if (FogParams.x > 0.5f)
    {
        float dist = distance(CameraPos.xyz, input.wPos);
        float fogFactor = saturate((dist - FogParams.y) / (FogParams.z - FogParams.y));
        color = lerp(color, FogColor.rgb, fogFactor * FogParams.w);
    }
    
    if (VignetteParams.x > 0.5f)
    {
        float2 d = uv - 0.5f;
        float v = length(d);
        float vig = smoothstep(VignetteParams.z, VignetteParams.z - VignetteParams.w, v);
        color = lerp(VignetteColor.rgb, color, vig + (1.0 - VignetteParams.y));
    }
    
    if (PosterizeParams.x > 0.5f)
    {
        float levels = PosterizeParams.y;
        color = floor(color * levels) / levels;
    }
    
    if (MonoParams.x > 0.5f)
    {
        float lum = dot(color, float3(0.299, 0.587, 0.114));
        float3 mono = lerp(float3(lum, lum, lum), MonoColor.rgb * lum, 0.5);
        color = lerp(color, mono, MonoParams.y);
    }
    
    float3 hsv = RGBtoHSV(color);
    hsv.y *= ColorCorrParams.x;
    color = HSVtoRGB(hsv);
    color = (color - 0.5f) * ColorCorrParams.y + 0.5f;
    color = pow(abs(color), 1.0f / ColorCorrParams.z);
    color += ColorCorrParams.w;
    
    if (ScanlineParams.x > 0.5f && ScanlineParams.w < 0.5f)
    {
        color = ApplyScanline(color, uv);
    }
    
    color = AceSToneMapping(color);
    
    if (ScanlineParams.x > 0.5f && ScanlineParams.w > 0.5f)
    {
        color = ApplyScanline(color, uv);
    }
    
    color = pow(abs(color), 1.0 / 2.2);

    return float4(saturate(color), texColor.a);
}