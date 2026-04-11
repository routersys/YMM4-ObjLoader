using System.Text;

namespace ObjLoader.Rendering.Shaders.Fx;

internal sealed class FxShaderAssembler
{
    private const string CbufDeclaration = """
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
        """;

    public string Assemble(string convertedBody, FxCollectedProperties properties)
    {
        ArgumentNullException.ThrowIfNull(convertedBody);
        ArgumentNullException.ThrowIfNull(properties);

        var sb = new StringBuilder(convertedBody.Length + 2048);

        sb.AppendLine(CbufDeclaration);
        sb.AppendLine();

        sb.AppendLine("Texture2D tex : register(t0);");
        sb.AppendLine("SamplerState sam : register(s0);");

        AppendUserTexturesAndSamplers(sb, properties);

        sb.AppendLine();
        sb.Append(convertedBody);

        return sb.ToString();
    }

    private static void AppendUserTexturesAndSamplers(StringBuilder sb, FxCollectedProperties properties)
    {
        var hasExtras = false;

        foreach (var (_, tex) in properties.Textures.OrderBy(kv => kv.Value.Slot))
        {
            if (tex.Slot <= 0) continue;
            sb.AppendLine($"Texture2D {tex.Name} : register(t{tex.Slot});");
            hasExtras = true;
        }

        foreach (var (_, sam) in properties.Samplers.OrderBy(kv => kv.Value.Slot))
        {
            if (sam.Slot <= 0) continue;
            sb.AppendLine($"SamplerState {sam.Name} : register(s{sam.Slot});");
            hasExtras = true;
        }

        if (hasExtras)
        {
            sb.AppendLine();
        }
    }
}