using System.Text;
using System.Text.RegularExpressions;

namespace ObjLoader.Rendering.Shaders
{
    public interface IShaderConverter
    {
        string Convert(string sourceCode);
    }

    public class HlslShaderConverter : IShaderConverter
    {
        private const string StandardCBuffer = @"
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
    float4 GridColor;
    float4 GridAxisColor;
}
";
        private const string StandardTextures = @"
Texture2D tex : register(t0);
SamplerState sam : register(s0);
";

        public string Convert(string sourceCode)
        {
            var sb = new StringBuilder();

            if (!Regex.IsMatch(sourceCode, @"cbuffer\s+CBuf") && !Regex.IsMatch(sourceCode, @"register\s*\(\s*b0\s*\)"))
            {
                sb.AppendLine(StandardCBuffer);
                sb.AppendLine();
            }

            if (!Regex.IsMatch(sourceCode, @"Texture2D\s+\w+") && !Regex.IsMatch(sourceCode, @"register\s*\(\s*t0\s*\)"))
            {
                sb.AppendLine(StandardTextures);
                sb.AppendLine();
            }

            var adaptedSource = AdaptEntryPoints(sourceCode);
            sb.AppendLine(adaptedSource);
            sb.AppendLine();

            return sb.ToString();
        }

        private string AdaptEntryPoints(string source)
        {
            bool hasTargetVs = Regex.IsMatch(source, @"(float4|PS_IN)\s+VS\s*\(");
            bool hasTargetPs = Regex.IsMatch(source, @"(float4)\s+PS\s*\(");

            if (hasTargetVs && hasTargetPs)
            {
                return source;
            }

            var sb = new StringBuilder(source);
            sb.AppendLine();

            if (!hasTargetVs)
            {
                if (Regex.IsMatch(source, @"void\s+vert\s*\("))
                {
                    sb.AppendLine(@"
PS_IN VS(VS_IN input) {
    PS_IN output;
    output.pos = mul(float4(input.pos, 1.0f), WorldViewProj);
    output.wPos = mul(float4(input.pos, 1.0f), World).xyz;
    output.norm = mul(float4(input.norm, 0.0f), World).xyz;
    output.uv = input.uv;
    vert(output.pos, output.norm, output.uv);
    return output;
}");
                }
                else
                {
                    sb.AppendLine(@"
struct VS_IN_DEFAULT { float3 pos : POSITION; float3 norm : NORMAL; float2 uv : TEXCOORD; };
struct PS_IN_DEFAULT { float4 pos : SV_POSITION; float3 wPos : TEXCOORD1; float3 norm : NORMAL; float2 uv : TEXCOORD0; };

PS_IN_DEFAULT VS(VS_IN_DEFAULT input) {
    PS_IN_DEFAULT output;
    output.pos = mul(float4(input.pos, 1.0f), WorldViewProj);
    output.wPos = mul(float4(input.pos, 1.0f), World).xyz;
    output.norm = mul(float4(input.norm, 0.0f), World).xyz;
    output.uv = input.uv;
    return output;
}");
                }
            }

            if (!hasTargetPs)
            {
                if (Regex.IsMatch(source, @"float4\s+frag\s*\("))
                {
                    sb.AppendLine(@"
float4 PS(PS_IN input) : SV_Target {
    return frag(input);
}");
                }
                else if (Regex.IsMatch(source, @"float4\s+main\s*\("))
                {
                    sb.AppendLine(@"
float4 PS(PS_IN input) : SV_Target {
    return main(input.pos, input.uv); 
}");
                }
                else
                {
                    sb.AppendLine(@"
float4 PS(PS_IN input) : SV_Target {
    return BaseColor;
}");
                }
            }

            return sb.ToString();
        }
    }
}