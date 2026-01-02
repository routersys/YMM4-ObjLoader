using Vortice.D3DCompiler;

namespace ObjLoader.Rendering
{
    internal static class ShaderStore
    {
        private static byte[]? _cachedVertexShaderByteCode;
        private static byte[]? _cachedPixelShaderByteCode;
        private static byte[]? _cachedGridPixelShaderByteCode;
        private static byte[]? _cachedGridVertexShaderByteCode;
        private static readonly object _lock = new object();

        public static (byte[] VS, byte[] PS, byte[] GridVS, byte[] GridPS) GetByteCodes()
        {
            lock (_lock)
            {
                if (_cachedVertexShaderByteCode == null)
                {
                    var vertexShaderCode = @"
                    cbuffer CBuf : register(b0) { 
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
                    }
                    struct VS_IN { float3 pos : POSITION; float3 norm : NORMAL; float2 uv : TEXCOORD; };
                    struct PS_IN { float4 pos : SV_POSITION; float3 wPos : TEXCOORD1; float3 norm : NORMAL; float2 uv : TEXCOORD0; };
                    PS_IN VS(VS_IN input) {
                        PS_IN output;
                        output.pos = mul(float4(input.pos, 1.0f), WorldViewProj);
                        output.wPos = mul(float4(input.pos, 1.0f), World).xyz;
                        output.norm = mul(float4(input.norm, 0.0f), World).xyz;
                        output.uv = input.uv;
                        return output;
                    }";

                    using var vsBlob = Compiler.Compile(vertexShaderCode, "VS", "ObjLoaderVS", "vs_5_0", ShaderFlags.OptimizationLevel3, EffectFlags.None);
                    _cachedVertexShaderByteCode = vsBlob.AsBytes();
                }

                if (_cachedPixelShaderByteCode == null)
                {
                    var pixelShaderCode = @"
                    cbuffer CBuf : register(b0) { 
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
                    }
                    Texture2D tex : register(t0);
                    SamplerState sam : register(s0);
                    struct PS_IN { float4 pos : SV_POSITION; float3 wPos : TEXCOORD1; float3 norm : NORMAL; float2 uv : TEXCOORD0; };
                    float4 PS(PS_IN input) : SV_Target {
                        float4 texColor = tex.Sample(sam, input.uv) * BaseColor;
                        
                        if (LightEnabled <= 0.5f) {
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
                    }";

                    using var psBlob = Compiler.Compile(pixelShaderCode, "PS", "ObjLoaderPS", "ps_5_0", ShaderFlags.OptimizationLevel3, EffectFlags.None);
                    _cachedPixelShaderByteCode = psBlob.AsBytes();
                }

                if (_cachedGridVertexShaderByteCode == null)
                {
                    var gridVS = @"
                    cbuffer CBuf : register(b0) { matrix WorldViewProj; matrix World; float4 CameraPos; }
                    struct VS_IN { float3 pos : POSITION; };
                    struct PS_IN { float4 pos : SV_POSITION; float3 wPos : TEXCOORD0; };
                    PS_IN VS(VS_IN input) {
                        PS_IN output;
                        output.pos = mul(float4(input.pos, 1.0f), WorldViewProj);
                        output.wPos = mul(float4(input.pos, 1.0f), World).xyz;
                        return output;
                    }";
                    using var gridVsBlob = Compiler.Compile(gridVS, "VS", "GridVS", "vs_5_0", ShaderFlags.OptimizationLevel3, EffectFlags.None);
                    _cachedGridVertexShaderByteCode = gridVsBlob.AsBytes();
                }

                if (_cachedGridPixelShaderByteCode == null)
                {
                    var gridPS = @"
                    cbuffer CBuf : register(b0) { matrix WorldViewProj; matrix World; float4 CameraPos; }
                    struct PS_IN { float4 pos : SV_POSITION; float3 wPos : TEXCOORD0; };
                    float4 PS(PS_IN input) : SV_Target {
                        float3 pos = input.wPos;
                        float2 coord = pos.xz;
                        float2 grid = abs(frac(coord - 0.5) - 0.5) / fwidth(coord);
                        float lineVal = min(grid.x, grid.y);
                        
                        float4 color = float4(0.5, 0.5, 0.5, 1.0 - min(lineVal, 1.0));
                        
                        float2 axis = abs(coord);
                        float2 axisWidth = 1.5 * fwidth(coord);
                        
                        if(axis.x < axisWidth.x || axis.y < axisWidth.y) {
                            color = float4(0.3, 0.3, 0.3, 1.0);
                        }

                        float dist = length(CameraPos.xz - pos.xz);
                        float scaleX = length(float3(World[0][0], World[0][1], World[0][2]));
                        if (scaleX > 0.5) {
                            color.a *= max(0.0, 1.0 - dist / 100.0);
                        }

                        return color;
                    }";
                    using var gridPsBlob = Compiler.Compile(gridPS, "PS", "GridPS", "ps_5_0", ShaderFlags.OptimizationLevel3, EffectFlags.None);
                    _cachedGridPixelShaderByteCode = gridPsBlob.AsBytes();
                }

                return (_cachedVertexShaderByteCode!, _cachedPixelShaderByteCode!, _cachedGridVertexShaderByteCode!, _cachedGridPixelShaderByteCode!);
            }
        }
    }
}