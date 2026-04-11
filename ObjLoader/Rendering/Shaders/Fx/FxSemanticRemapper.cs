using System.Text;
using System.Text.RegularExpressions;

namespace ObjLoader.Rendering.Shaders.Fx;

internal sealed class FxSemanticRemapper
{
    private static readonly Regex StringVarPattern = new(
        @"^\s*string\s+\w+\s*(?:=\s*(?:""[^""]*""|'[^']*'|[^;])*)?;[ \t]*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex TextureDeclPattern = new(
        @"(?:texture2D|texture)\s+(\w+)\s*(?::\s*\w+)?\s*;",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SamplerStateDeclPattern = new(
        @"(?:sampler2D|sampler)\s+(\w+)\s*=\s*sampler_state\s*\{[^}]*\}\s*;",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex MatrixSemanticPattern = new(
        @"float4x4\s+(\w+)\s*:\s*(WORLDVIEWPROJECTION|WORLDVIEW|WORLD|WORLDINVERSE|WORLDTRANSPOSE|VIEW|PROJECTION|VIEWINVERSE)\s*;",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex VectorSemanticWithObjectPattern = new(
        @"(float4|float3|float2|float)\s+(\w+)\s*:\s*(DIFFUSE|AMBIENT|EMISSIVE|SPECULAR|SPECULARPOWER|TOONCOLOR|EDGECOLOR|DIRECTION|ADDINGTEXTURE|MULTIPLYINGTEXTURE|ADDINGSPHERETEXTURE|MULTIPLYINGSPHERETEXTURE)\s+<\s*string\s+Object\s*=\s*""(\w+)""\s*[^>]*>\s*;",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex VectorSemanticPattern = new(
        @"(float4|float3|float2|float)\s+(\w+)\s*:\s*(DIFFUSE|AMBIENT|EMISSIVE|SPECULAR|SPECULARPOWER|TOONCOLOR|EDGECOLOR|DIRECTION|POSITION|ADDINGTEXTURE|MULTIPLYINGTEXTURE|ADDINGSPHERETEXTURE|MULTIPLYINGSPHERETEXTURE|VIEWPORTPIXELSIZE)\s*;",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ControlObjectPattern = new(
        @"(float4x4|float4|float3|float2|float|bool|int)\s+(\w+)\s*:\s*CONTROLOBJECT\s*;",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PlainBoolFlagPattern = new(
        @"^\s*bool\s+(use_texture|use_spheremap|use_toon|use_subtexture|parthf|transp|spadd)\s*;[ \t]*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex SharedModifierPattern = new(@"\bshared\s+", RegexOptions.Compiled);
    private static readonly Regex InlineModifierPattern = new(@"\binline\s+", RegexOptions.Compiled);
    private static readonly Regex AnyStringStatementPattern = new(
        @"\bstring\b[^;\n]*;",
        RegexOptions.Compiled);

    private static readonly Regex OutputColorSemanticPattern = new(
        @"\)\s*:\s*COLOR\d*\b",
        RegexOptions.Compiled);

    private static readonly Regex MmdCatchAllSemanticPattern = new(
        @"^[ \t]*(?:float4x4|float4|float3|float2|float|bool|int)\s+\w+\s*:\s*(?:STANDARDSGLOBAL|OFFSCREENRENDERTARGET|RENDEREDRESULTTEXTURE|MOUSEPOSITION|LEFTMOUSEDOWN|RIGHTMOUSEDOWN|MIDDLEMOUSEDOWN|TIME|ELAPSEDTIME|GAMETIMERESET|SYSTIME|ELAPSEDTIMERESET|CONTROLOBJECT|WORLDVIEWPROJECTION|WORLD|VIEW|PROJECTION|DIRECTION|DIFFUSE|AMBIENT|SPECULAR|EMISSIVE|SPECULARPOWER|TOONCOLOR|EDGECOLOR|ADDINGTEXTURE|MULTIPLYINGTEXTURE|ADDINGSPHERETEXTURE|MULTIPLYINGSPHERETEXTURE|VIEWPORTPIXELSIZE|POSITION)\b[^;]*;[ \t]*$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, string> MatrixSemanticMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WORLDVIEWPROJECTION"] = "WorldViewProj",
        ["WORLDVIEW"] = "WorldViewProj",
        ["WORLD"] = "World",
        ["WORLDINVERSE"] = "World",
        ["WORLDTRANSPOSE"] = "World",
        ["VIEW"] = "WorldViewProj",
        ["VIEWINVERSE"] = "WorldViewProj",
        ["PROJECTION"] = "WorldViewProj",
    };

    private static readonly Dictionary<string, string> VectorSemanticMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DIFFUSE_Geometry"] = "BaseColor",
        ["DIFFUSE_Light"] = "float4(LightColor.rgb, 1.0f)",
        ["AMBIENT_Geometry"] = "float4(AmbientColor.rgb, 1.0f)",
        ["AMBIENT_Light"] = "float4(AmbientColor.rgb, 1.0f)",
        ["EMISSIVE_Geometry"] = "float4(0.0f, 0.0f, 0.0f, 0.0f)",
        ["SPECULAR_Geometry"] = "float4(1.0f, 1.0f, 1.0f, 1.0f)",
        ["SPECULAR_Light"] = "float4(1.0f, 1.0f, 1.0f, 1.0f)",
        ["SPECULARPOWER_Geometry"] = "Shininess",
        ["TOONCOLOR"] = "float3(0.5f, 0.5f, 0.5f)",
        ["EDGECOLOR"] = "float4(0.0f, 0.0f, 0.0f, 1.0f)",
        ["DIRECTION_Light"] = "normalize(-LightPos.xyz)",
        ["POSITION_Camera"] = "CameraPos.xyz",
        ["ADDINGTEXTURE"] = "float4(0.0f, 0.0f, 0.0f, 0.0f)",
        ["MULTIPLYINGTEXTURE"] = "float4(1.0f, 1.0f, 1.0f, 1.0f)",
        ["ADDINGSPHERETEXTURE"] = "float4(0.0f, 0.0f, 0.0f, 0.0f)",
        ["MULTIPLYINGSPHERETEXTURE"] = "float4(1.0f, 1.0f, 1.0f, 1.0f)",
        ["VIEWPORTPIXELSIZE"] = "float2(1920.0f, 1080.0f)",
    };

    private static readonly Dictionary<string, string> BoolFlagDefaults = new(StringComparer.Ordinal)
    {
        ["use_texture"] = "true",
        ["use_spheremap"] = "false",
        ["use_toon"] = "false",
        ["use_subtexture"] = "false",
        ["parthf"] = "false",
        ["transp"] = "false",
        ["spadd"] = "false",
    };

    public string Remap(string source, FxCollectedProperties properties)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(properties);

        var (global, structs) = ExtractStructBlocks(source);

        global = RemoveTechniqueBlocks(global);
        global = VectorSemanticWithObjectPattern.Replace(global, SubstituteVectorSemanticWithObject);
        global = RemoveAnnotationsSafe(global);
        global = ControlObjectPattern.Replace(global, SubstituteControlObject);
        global = MatrixSemanticPattern.Replace(global, SubstituteMatrixSemantic);
        global = VectorSemanticPattern.Replace(global, SubstituteVectorSemantic);
        global = StringVarPattern.Replace(global, string.Empty);
        global = TextureDeclPattern.Replace(global, m => SubstituteTexture(m, properties));
        global = SamplerStateDeclPattern.Replace(global, m => SubstituteSampler(m, properties));
        global = PlainBoolFlagPattern.Replace(global, SubstituteBoolFlag);
        global = MmdCatchAllSemanticPattern.Replace(global, string.Empty);
        global = SharedModifierPattern.Replace(global, string.Empty);
        global = InlineModifierPattern.Replace(global, string.Empty);
        global = AnyStringStatementPattern.Replace(global, string.Empty);
        global = OutputColorSemanticPattern.Replace(global, ") : SV_Target");

        return RestoreStructBlocks(global, structs, properties.VsOutputType);
    }

    private static (string global, List<(string placeholder, string definition, string name)> structs)
        ExtractStructBlocks(string source)
    {
        var structs = new List<(string, string, string)>();
        var sb = new StringBuilder(source.Length);
        var i = 0;
        var counter = 0;

        while (i < source.Length)
        {
            var structIdx = FindKeyword(source, "struct", i);
            if (structIdx < 0)
            {
                sb.Append(source, i, source.Length - i);
                break;
            }

            var nameStart = structIdx + 6;
            while (nameStart < source.Length && char.IsWhiteSpace(source[nameStart])) nameStart++;
            var nameEnd = nameStart;
            while (nameEnd < source.Length && IsIdentChar(source[nameEnd])) nameEnd++;
            var structName = source[nameStart..nameEnd];

            var braceIdx = source.IndexOf('{', nameEnd);
            if (braceIdx < 0)
            {
                sb.Append(source, i, source.Length - i);
                break;
            }

            var closeBrace = FindMatchingBrace(source, braceIdx);
            if (closeBrace < 0)
            {
                sb.Append(source, i, source.Length - i);
                break;
            }

            var endPos = closeBrace + 1;
            if (endPos < source.Length && source[endPos] == ';') endPos++;

            sb.Append(source, i, structIdx - i);

            var placeholder = $"\x02STRUCT{counter++}\x03";
            structs.Add((placeholder, source[structIdx..endPos], structName));
            sb.Append(placeholder);

            i = endPos;
        }

        return (sb.ToString(), structs);
    }

    private static string RestoreStructBlocks(
        string global,
        List<(string placeholder, string definition, string name)> structs,
        string vsOutputType)
    {
        var sb = new StringBuilder(global);

        foreach (var (placeholder, definition, name) in structs)
        {
            var restored = definition;

            if (!string.IsNullOrEmpty(vsOutputType) &&
                string.Equals(name, vsOutputType, StringComparison.Ordinal))
            {
                restored = Regex.Replace(restored,
                    @"(:\s*)POSITION\b",
                    "$1SV_POSITION",
                    RegexOptions.Compiled);
            }

            sb.Replace(placeholder, restored);
        }

        return sb.ToString();
    }

    private static string RemoveTechniqueBlocks(string source)
    {
        var sb = new StringBuilder(source.Length);
        var i = 0;

        while (i < source.Length)
        {
            var techIdx = FindKeyword(source, "technique", i);
            if (techIdx < 0)
            {
                sb.Append(source, i, source.Length - i);
                break;
            }

            var braceIdx = source.IndexOf('{', techIdx + 9);
            if (braceIdx < 0)
            {
                sb.Append(source, i, source.Length - i);
                break;
            }

            var closeIdx = FindMatchingBrace(source, braceIdx);
            if (closeIdx < 0)
            {
                sb.Append(source, i, source.Length - i);
                break;
            }

            sb.Append(source, i, techIdx - i);
            i = closeIdx + 1;
        }

        return sb.ToString();
    }

    private static string RemoveAnnotationsSafe(string source)
    {
        var sb = new StringBuilder(source.Length);
        var i = 0;

        while (i < source.Length)
        {
            if (source[i] == '<' && IsAnnotationStart(source, i + 1))
            {
                var end = FindAnnotationClose(source, i + 1);
                if (end >= 0)
                {
                    i = end + 1;
                    continue;
                }
            }
            sb.Append(source[i]);
            i++;
        }

        return sb.ToString();
    }

    private static bool IsAnnotationStart(string source, int pos)
    {
        while (pos < source.Length && char.IsWhiteSpace(source[pos])) pos++;
        if (pos >= source.Length) return false;

        ReadOnlySpan<string> starters = ["string", "float4x4", "float4", "float3", "float2", "float", "int", "bool", ">"];
        var span = source.AsSpan(pos);
        foreach (var kw in starters)
        {
            if (span.StartsWith(kw.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static int FindAnnotationClose(string source, int start)
    {
        var i = start;
        while (i < source.Length)
        {
            switch (source[i])
            {
                case '"':
                    i++;
                    while (i < source.Length && source[i] != '"') i++;
                    if (i < source.Length) i++;
                    break;
                case '\'':
                    i++;
                    while (i < source.Length && source[i] != '\'') i++;
                    if (i < source.Length) i++;
                    break;
                case '>':
                    return i;
                default:
                    i++;
                    break;
            }
        }
        return -1;
    }

    private static string SubstituteControlObject(Match m)
    {
        var type = m.Groups[1].Value;
        var varName = m.Groups[2].Value;
        return $"static {type} {varName} = {GetDefaultValue(type)};";
    }

    private static string SubstituteMatrixSemantic(Match m)
    {
        var varName = m.Groups[1].Value;
        var semantic = m.Groups[2].Value;
        var cbufMember = MatrixSemanticMap.GetValueOrDefault(semantic, "WorldViewProj");
        return $"static float4x4 {varName} = {cbufMember};";
    }

    private static string SubstituteVectorSemanticWithObject(Match m)
    {
        var type = m.Groups[1].Value;
        var varName = m.Groups[2].Value;
        var semantic = m.Groups[3].Value;
        var objectVal = m.Groups[4].Value;

        var key = $"{semantic}_{objectVal}";
        if (!VectorSemanticMap.TryGetValue(key, out var value) &&
            !VectorSemanticMap.TryGetValue(semantic, out value))
        {
            return $"static {type} {varName} = ({type})0;";
        }
        return $"static {type} {varName} = ({type})({value});";
    }

    private static string SubstituteVectorSemantic(Match m)
    {
        var type = m.Groups[1].Value;
        var varName = m.Groups[2].Value;
        var semantic = m.Groups[3].Value;

        if (!VectorSemanticMap.TryGetValue(semantic, out var value))
        {
            return $"static {type} {varName} = ({type})0;";
        }
        return $"static {type} {varName} = ({type})({value});";
    }

    private static string SubstituteTexture(Match m, FxCollectedProperties properties)
    {
        var name = m.Groups[1].Value;
        if (!properties.Textures.TryGetValue(name, out var tex)) return string.Empty;
        return tex.Slot switch
        {
            < 0 => string.Empty,
            0 => string.Empty,
            _ => $"Texture2D {name} : register(t{tex.Slot});"
        };
    }

    private static string SubstituteSampler(Match m, FxCollectedProperties properties)
    {
        var name = m.Groups[1].Value;
        if (!properties.Samplers.TryGetValue(name, out var sam)) return string.Empty;
        return sam.Slot switch
        {
            < 0 => string.Empty,
            0 => string.Empty,
            _ => $"SamplerState {name} : register(s{sam.Slot});"
        };
    }

    private static string SubstituteBoolFlag(Match m)
    {
        var name = m.Groups[1].Value;
        var value = BoolFlagDefaults.GetValueOrDefault(name, "false");
        return $"static bool {name} = {value};";
    }

    private static string ConvertStructurePositionSemantics(string source, string vsOutputType)
    {
        if (string.IsNullOrEmpty(vsOutputType)) return source;

        var sb = new StringBuilder(source.Length);
        var i = 0;

        while (i < source.Length)
        {
            var structIdx = FindKeyword(source, "struct", i);
            if (structIdx < 0)
            {
                sb.Append(source, i, source.Length - i);
                break;
            }

            var nameStart = structIdx + 6;
            while (nameStart < source.Length && char.IsWhiteSpace(source[nameStart])) nameStart++;
            var nameEnd = nameStart;
            while (nameEnd < source.Length && IsIdentChar(source[nameEnd])) nameEnd++;
            var structName = source[nameStart..nameEnd];

            var braceIdx = source.IndexOf('{', nameEnd);
            if (braceIdx < 0)
            {
                sb.Append(source, i, source.Length - i);
                break;
            }

            var closeBrace = FindMatchingBrace(source, braceIdx);
            if (closeBrace < 0)
            {
                sb.Append(source, i, source.Length - i);
                break;
            }

            sb.Append(source, i, braceIdx - i);

            var structContent = source[braceIdx..(closeBrace + 1)];

            if (string.Equals(structName, vsOutputType, StringComparison.Ordinal))
            {
                var converted = Regex.Replace(structContent,
                    @"(:\s*)POSITION\b",
                    "$1SV_POSITION",
                    RegexOptions.Compiled);
                sb.Append(converted);
            }
            else
            {
                sb.Append(structContent);
            }

            i = closeBrace + 1;
        }

        return sb.ToString();
    }

    private static int FindKeyword(string source, string keyword, int start)
    {
        var idx = start;
        while (idx < source.Length)
        {
            var found = source.IndexOf(keyword, idx, StringComparison.Ordinal);
            if (found < 0) return -1;

            var before = found > 0 ? source[found - 1] : ' ';
            var after = found + keyword.Length < source.Length ? source[found + keyword.Length] : ' ';
            if (!IsIdentChar(before) && !IsIdentChar(after)) return found;

            idx = found + 1;
        }
        return -1;
    }

    private static int FindMatchingBrace(string source, int openBrace)
    {
        var depth = 1;
        var i = openBrace + 1;
        while (i < source.Length && depth > 0)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}') depth--;
            if (depth > 0) i++;
        }
        return depth == 0 ? i : -1;
    }

    private static bool IsIdentChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    private static string GetDefaultValue(string type) => type.ToLowerInvariant() switch
    {
        "bool" => "false",
        "int" => "0",
        "float" => "0.0f",
        "float2" => "float2(0.0f, 0.0f)",
        "float3" => "float3(0.0f, 0.0f, 0.0f)",
        "float4" => "float4(0.0f, 0.0f, 0.0f, 0.0f)",
        "float4x4" => "(float4x4)0",
        _ => "0"
    };
}