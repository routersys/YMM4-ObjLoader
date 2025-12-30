using Vortice.D3DCompiler;

namespace ObjLoader.Rendering
{
    internal static class ShaderStore
    {
        private static byte[]? _cachedVertexShaderByteCode;
        private static byte[]? _cachedPixelShaderByteCode;
        private static readonly object _lock = new object();

        public static (byte[] VS, byte[] PS) GetByteCodes()
        {
            if (_cachedVertexShaderByteCode != null && _cachedPixelShaderByteCode != null)
            {
                return (_cachedVertexShaderByteCode, _cachedPixelShaderByteCode);
            }

            lock (_lock)
            {
                if (_cachedVertexShaderByteCode != null && _cachedPixelShaderByteCode != null)
                {
                    return (_cachedVertexShaderByteCode, _cachedPixelShaderByteCode);
                }

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

                        float3 ambient = texColor.rgb * AmbientColor.rgb;

                        float3 n = normalize(input.norm);
                        float3 lightDir = normalize(LightPos.xyz - input.wPos);
                        float diff = max(dot(n, lightDir), 0.0f);
                        float3 diffuse = texColor.rgb * LightColor.rgb * diff * DiffuseIntensity;

                        float3 viewDir = normalize(CameraPos.xyz - input.wPos);
                        float3 halfDir = normalize(lightDir + viewDir);
                        float spec = pow(max(dot(n, halfDir), 0.0f), Shininess);
                        float3 specular = LightColor.rgb * spec * SpecularIntensity;

                        return float4(ambient + diffuse + specular, texColor.a);
                    }";

                using var vsBlob = Compiler.Compile(vertexShaderCode, "VS", "ObjLoaderVS", "vs_5_0", ShaderFlags.OptimizationLevel3, EffectFlags.None);
                _cachedVertexShaderByteCode = vsBlob.AsBytes();

                using var psBlob = Compiler.Compile(pixelShaderCode, "PS", "ObjLoaderPS", "ps_5_0", ShaderFlags.OptimizationLevel3, EffectFlags.None);
                _cachedPixelShaderByteCode = psBlob.AsBytes();

                return (_cachedVertexShaderByteCode, _cachedPixelShaderByteCode);
            }
        }
    }
}