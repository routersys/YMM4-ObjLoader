using ObjLoader.Localization;
using System.Text;

namespace ObjLoader.Rendering.Shaders;

public interface IShaderConverter
{
    string Convert(string sourceCode);
}

public sealed class HlslShaderConverter : IShaderConverter
{
    private const string StandardCBuffer = """
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

    private const string StandardTextures = """
        Texture2D tex : register(t0);
        SamplerState sam : register(s0);
        """;

    private const string DefaultVSINStruct = """
        struct VS_IN { float3 pos : POSITION; float3 norm : NORMAL; float2 uv : TEXCOORD; };
        """;

    private const string DefaultPSINStruct = """
        struct PS_IN { float4 pos : SV_POSITION; float3 wPos : TEXCOORD1; float3 norm : NORMAL; float2 uv : TEXCOORD0; };
        """;

    private const string DefaultVS = """
        PS_IN VS(VS_IN input) {
            PS_IN output;
            output.pos = mul(float4(input.pos, 1.0f), WorldViewProj);
            output.wPos = mul(float4(input.pos, 1.0f), World).xyz;
            output.norm = mul(float4(input.norm, 0.0f), World).xyz;
            output.uv = input.uv;
            return output;
        }
        """;

    private const string DefaultPS = """
        float4 PS(PS_IN input) : SV_Target {
            return BaseColor;
        }
        """;

    public string Convert(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException(Texts.ShaderConversion_SourceCodeEmpty, nameof(sourceCode));
        }

        try
        {
            var parser = new HlslParser(sourceCode);
            var ast = parser.Parse();
            return BuildShaderCode(ast);
        }
        catch (HlslParseException ex)
        {
            throw new ShaderConversionException(
                string.Format(Texts.ShaderConversion_ParsingFailed, ex.Message, ex.Line, ex.Column),
                ex);
        }
        catch (Exception ex) when (ex is not ShaderConversionException)
        {
            throw new ShaderConversionException(
                string.Format(Texts.ShaderConversion_ConversionFailed, ex.Message),
                ex);
        }
    }

    private static string BuildShaderCode(ShaderAst ast)
    {
        var builder = new StringBuilder(4096);

        AppendPreprocessorDirectives(builder, ast);
        AppendStandardResources(builder, ast);
        AppendTypedefs(builder, ast);
        AppendStructures(builder, ast);
        AppendConstantBuffers(builder, ast);
        AppendGlobalVariables(builder, ast);
        AppendHelperFunctions(builder, ast);
        AppendVertexShader(builder, ast);
        AppendPixelShader(builder, ast);
        AppendGeometryShader(builder, ast);
        AppendHullShader(builder, ast);
        AppendDomainShader(builder, ast);
        AppendComputeShader(builder, ast);

        return builder.ToString();
    }

    private static void AppendPreprocessorDirectives(StringBuilder builder, ShaderAst ast)
    {
        if (ast.PreprocessorDirectives.Count == 0)
        {
            return;
        }

        foreach (var directive in ast.PreprocessorDirectives)
        {
            builder.AppendLine(directive);
        }
        builder.AppendLine();
    }

    private static void AppendStandardResources(StringBuilder builder, ShaderAst ast)
    {
        if (!ast.HasConstantBuffer("CBuf"))
        {
            builder.AppendLine(StandardCBuffer);
            builder.AppendLine();
        }

        if (!ast.HasTexture("tex"))
        {
            builder.AppendLine(StandardTextures);
            builder.AppendLine();
        }

        AppendUserResources(builder, ast);

        var hasVSIN = ast.HasStruct("VS_IN");
        var hasPSIN = ast.HasStruct("PS_IN");

        if (!hasVSIN)
        {
            builder.AppendLine(DefaultVSINStruct);
        }
        if (!hasPSIN)
        {
            builder.AppendLine(DefaultPSINStruct);
        }
        if (!hasVSIN || !hasPSIN)
        {
            builder.AppendLine();
        }
    }

    private static void AppendUserResources(StringBuilder builder, ShaderAst ast)
    {
        var nextTextureSlot = 1;
        var nextSamplerSlot = 1;

        foreach (var variable in ast.GlobalVariables)
        {
            if (string.Equals(variable.Name, "tex", StringComparison.Ordinal) ||
                string.Equals(variable.Name, "sam", StringComparison.Ordinal))
            {
                continue;
            }

            var isTexture = variable.Type.Contains("Texture", StringComparison.Ordinal);
            var isSampler = variable.Type.Contains("Sampler", StringComparison.Ordinal);

            if (!isTexture && !isSampler)
            {
                continue;
            }

            builder.Append(variable.Type);
            builder.Append(' ');
            builder.Append(variable.Name);

            if (variable.RegisterSlot is not null)
            {
                builder.Append(" : register(");
                builder.Append(variable.RegisterSlot);
                builder.Append(')');
            }
            else if (isTexture)
            {
                builder.Append(" : register(t");
                builder.Append(nextTextureSlot);
                builder.Append(')');
                nextTextureSlot++;
            }
            else if (isSampler)
            {
                builder.Append(" : register(s");
                builder.Append(nextSamplerSlot);
                builder.Append(')');
                nextSamplerSlot++;
            }

            builder.AppendLine(";");
        }

        var userTextures = ast.GetTextures();
        var userSamplers = ast.GetSamplers();
        if (userTextures.Count > 0 || userSamplers.Count > 0)
        {
            builder.AppendLine();
        }
    }

    private static void AppendTypedefs(StringBuilder builder, ShaderAst ast)
    {
        if (ast.Typedefs.Count == 0)
        {
            return;
        }

        foreach (var td in ast.Typedefs)
        {
            builder.Append("typedef ");
            builder.Append(td.OriginalType);
            builder.Append(' ');
            builder.Append(td.AliasName);
            builder.AppendLine(";");
        }
        builder.AppendLine();
    }

    private static void AppendStructures(StringBuilder builder, ShaderAst ast)
    {
        foreach (var structDef in ast.Structures)
        {
            builder.Append("struct ");
            builder.Append(structDef.Name);
            builder.AppendLine(" {");

            foreach (var member in structDef.Members)
            {
                builder.Append("    ");
                builder.Append(member.Type);
                builder.Append(' ');
                builder.Append(member.Name);

                if (member.Semantic is not null)
                {
                    builder.Append(" : ");
                    builder.Append(member.Semantic);
                }

                builder.AppendLine(";");
            }

            builder.AppendLine("};");
            builder.AppendLine();
        }
    }

    private static void AppendConstantBuffers(StringBuilder builder, ShaderAst ast)
    {
        foreach (var cbuffer in ast.ConstantBuffers)
        {
            builder.Append("cbuffer ");
            builder.Append(cbuffer.Name);
            builder.AppendLine(" {");

            var bodyLines = cbuffer.Body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in bodyLines)
            {
                var trimmedLine = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    builder.Append("    ");
                    builder.AppendLine(trimmedLine);
                }
            }

            builder.AppendLine("};");
            builder.AppendLine();
        }
    }

    private static void AppendGlobalVariables(StringBuilder builder, ShaderAst ast)
    {
        var nonResourceVars = new List<VariableDeclaration>();
        foreach (var variable in ast.GlobalVariables)
        {
            if (variable.Type.Contains("Texture", StringComparison.Ordinal) ||
                variable.Type.Contains("Sampler", StringComparison.Ordinal))
            {
                continue;
            }
            nonResourceVars.Add(variable);
        }

        if (nonResourceVars.Count == 0)
        {
            return;
        }

        foreach (var variable in nonResourceVars)
        {
            builder.Append(variable.Type);
            builder.Append(' ');
            builder.Append(variable.Name);

            if (variable.Semantic is not null)
            {
                builder.Append(" : ");
                builder.Append(variable.Semantic);
            }

            builder.AppendLine(";");
        }
        builder.AppendLine();
    }

    private static readonly HashSet<string> ShaderEntryPoints = new(StringComparer.Ordinal)
    {
        "VS", "PS", "GS", "HS", "DS", "CS",
        "vert", "frag", "geom", "hull", "domain", "compute", "main"
    };

    private static void AppendHelperFunctions(StringBuilder builder, ShaderAst ast)
    {
        foreach (var function in ast.Functions)
        {
            if (ShaderEntryPoints.Contains(function.Name))
            {
                continue;
            }

            AppendFunctionAttributes(builder, function);
            AppendFunctionSignature(builder, function);

            if (string.IsNullOrWhiteSpace(function.Body))
            {
                builder.AppendLine(";");
            }
            else
            {
                builder.AppendLine(" {");
                AppendFunctionBody(builder, function.Body);
                builder.AppendLine("}");
            }
            builder.AppendLine();
        }
    }

    private static void AppendFunctionAttributes(StringBuilder builder, FunctionDefinition function)
    {
        foreach (var attr in function.Attributes)
        {
            builder.Append('[');
            builder.Append(attr.Name);
            if (!string.IsNullOrEmpty(attr.Arguments))
            {
                builder.Append('(');
                builder.Append(attr.Arguments);
                builder.Append(')');
            }
            builder.AppendLine("]");
        }
    }

    private static void AppendFunctionSignature(StringBuilder builder, FunctionDefinition function)
    {
        builder.Append(function.ReturnType);
        builder.Append(' ');
        builder.Append(function.Name);
        builder.Append('(');

        for (var i = 0; i < function.Parameters.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var param = function.Parameters[i];
            builder.Append(param.Type);
            builder.Append(' ');
            builder.Append(param.Name);

            if (param.Semantic is not null)
            {
                builder.Append(" : ");
                builder.Append(param.Semantic);
            }
        }

        builder.Append(')');

        if (function.ReturnSemantic is not null)
        {
            builder.Append(" : ");
            builder.Append(function.ReturnSemantic);
        }
    }

    private static void AppendFunctionBody(StringBuilder builder, string body)
    {
        var bodyLines = body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in bodyLines)
        {
            var trimmedLine = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedLine))
            {
                builder.Append("    ");
                builder.AppendLine(trimmedLine);
            }
        }
    }

    private static void AppendFullFunction(StringBuilder builder, FunctionDefinition function)
    {
        AppendFunctionAttributes(builder, function);
        AppendFunctionSignature(builder, function);
        builder.AppendLine(" {");
        AppendFunctionBody(builder, function.Body);
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendVertexShader(StringBuilder builder, ShaderAst ast)
    {
        var vsFunc = ast.GetFunction("VS");
        if (vsFunc is not null)
        {
            AppendFullFunction(builder, vsFunc);
            return;
        }

        if (ast.HasFunction("vert"))
        {
            builder.AppendLine("""
                PS_IN VS(VS_IN input) {
                    PS_IN output;
                    output.pos = mul(float4(input.pos, 1.0f), WorldViewProj);
                    output.wPos = mul(float4(input.pos, 1.0f), World).xyz;
                    output.norm = mul(float4(input.norm, 0.0f), World).xyz;
                    output.uv = input.uv;
                    vert(output.pos, output.norm, output.uv);
                    return output;
                }
                """);
            builder.AppendLine();
            return;
        }

        builder.AppendLine(DefaultVS);
        builder.AppendLine();
    }

    private static void AppendPixelShader(StringBuilder builder, ShaderAst ast)
    {
        var psFunc = ast.GetFunction("PS");
        if (psFunc is not null)
        {
            AppendFullFunction(builder, psFunc);
            return;
        }

        if (ast.HasFunction("frag"))
        {
            var fragFunc = ast.GetFunction("frag")!;
            var hasInputParam = fragFunc.Parameters.Any(p =>
                p.Type.Contains("PS_IN", StringComparison.Ordinal));

            if (hasInputParam)
            {
                builder.AppendLine("""
                    float4 PS(PS_IN input) : SV_Target {
                        return frag(input);
                    }
                    """);
            }
            else
            {
                builder.AppendLine("""
                    float4 PS(PS_IN input) : SV_Target {
                        return frag(input.pos, input.uv);
                    }
                    """);
            }
            builder.AppendLine();
            return;
        }

        if (ast.HasFunction("main"))
        {
            var mainFunc = ast.GetFunction("main")!;

            if (mainFunc.Parameters.Count == 1 &&
                mainFunc.Parameters[0].Type.Contains("float2", StringComparison.Ordinal))
            {
                builder.AppendLine("""
                    float4 PS(PS_IN input) : SV_Target {
                        return main(input.uv);
                    }
                    """);
            }
            else
            {
                builder.AppendLine("""
                    float4 PS(PS_IN input) : SV_Target {
                        return main(input.pos, input.uv);
                    }
                    """);
            }
            builder.AppendLine();
            return;
        }

        builder.AppendLine(DefaultPS);
        builder.AppendLine();
    }

    private static void AppendGeometryShader(StringBuilder builder, ShaderAst ast)
    {
        var gsFunc = ast.GetFunction("GS");
        if (gsFunc is not null)
        {
            AppendFullFunction(builder, gsFunc);
            return;
        }

        var geomFunc = ast.GetFunction("geom");
        if (geomFunc is not null)
        {
            AppendFullFunction(builder, geomFunc with { Name = "GS" });
        }
    }

    private static void AppendHullShader(StringBuilder builder, ShaderAst ast)
    {
        var hsFunc = ast.GetFunction("HS");
        if (hsFunc is not null)
        {
            AppendFullFunction(builder, hsFunc);
            return;
        }

        var hullFunc = ast.GetFunction("hull");
        if (hullFunc is not null)
        {
            AppendFullFunction(builder, hullFunc with { Name = "HS" });
        }
    }

    private static void AppendDomainShader(StringBuilder builder, ShaderAst ast)
    {
        var dsFunc = ast.GetFunction("DS");
        if (dsFunc is not null)
        {
            AppendFullFunction(builder, dsFunc);
            return;
        }

        var domainFunc = ast.GetFunction("domain");
        if (domainFunc is not null)
        {
            AppendFullFunction(builder, domainFunc with { Name = "DS" });
        }
    }

    private static void AppendComputeShader(StringBuilder builder, ShaderAst ast)
    {
        var csFunc = ast.GetFunction("CS");
        if (csFunc is not null)
        {
            AppendFullFunction(builder, csFunc);
            return;
        }

        var computeFunc = ast.GetFunction("compute");
        if (computeFunc is not null)
        {
            AppendFullFunction(builder, computeFunc with { Name = "CS" });
        }
    }
}

public sealed class ShaderConversionException : Exception
{
    public ShaderConversionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(Texts.ShaderConversion_ArgumentNull, nameof(message));
        }
    }
}