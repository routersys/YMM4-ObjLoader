using System.Text;

namespace ObjLoader.Rendering.Shaders.Fx;

internal sealed class FxSamplerCallConverter
{
    private static readonly HashSet<string> MmdSystemSamplerNames = new(StringComparer.Ordinal)
    {
        "DefSampler",
        "ScreenShadowMapProcessedSamp",
        "ExShadowSSAOMapSamp",
    };

    public string Convert(string source, FxCollectedProperties properties)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(properties);

        return ReplaceTex2DCalls(source, (samplerName, uvExpr) =>
        {
            if (MmdSystemSamplerNames.Contains(samplerName))
            {
                return "float4(1.0f, 1.0f, 1.0f, 1.0f)";
            }

            if (properties.Samplers.TryGetValue(samplerName, out var sam))
            {
                if (sam.Slot < 0) return "float4(1.0f, 1.0f, 1.0f, 1.0f)";

                if (sam.Slot == 0) return $"tex.Sample(sam, {uvExpr})";

                var texName = properties.ResolveTextureName(samplerName) ?? "tex";
                return $"{texName}.Sample({samplerName}, {uvExpr})";
            }

            return $"tex.Sample(sam, {uvExpr})";
        });
    }

    private static string ReplaceTex2DCalls(string source, Func<string, string, string> replacer)
    {
        const string callPrefix = "tex2D";
        var sb = new StringBuilder(source.Length);
        var i = 0;

        while (i < source.Length)
        {
            var matchIdx = source.IndexOf(callPrefix, i, StringComparison.Ordinal);
            if (matchIdx < 0)
            {
                sb.Append(source, i, source.Length - i);
                break;
            }

            var before = matchIdx > 0 ? source[matchIdx - 1] : ' ';
            var afterEnd = matchIdx + callPrefix.Length;
            var after = afterEnd < source.Length ? source[afterEnd] : ' ';

            if (IsIdentChar(before) || IsIdentChar(after))
            {
                sb.Append(source, i, matchIdx - i + 1);
                i = matchIdx + 1;
                continue;
            }

            var openParen = afterEnd;
            while (openParen < source.Length && char.IsWhiteSpace(source[openParen])) openParen++;

            if (openParen >= source.Length || source[openParen] != '(')
            {
                sb.Append(source, i, afterEnd - i);
                i = afterEnd;
                continue;
            }

            var contentStart = openParen + 1;
            var closeParen = FindMatchingParen(source, openParen);
            if (closeParen < 0)
            {
                sb.Append(source, i, afterEnd - i);
                i = afterEnd;
                continue;
            }

            var inner = source[contentStart..closeParen];
            var (samplerName, uvExpr) = SplitFirstArg(inner);

            if (samplerName is null)
            {
                sb.Append(source, i, closeParen - i + 1);
                i = closeParen + 1;
                continue;
            }

            sb.Append(source, i, matchIdx - i);
            sb.Append(replacer(samplerName.Trim(), uvExpr?.Trim() ?? string.Empty));
            i = closeParen + 1;
        }

        return sb.ToString();
    }

    private static (string? samplerName, string? uvExpr) SplitFirstArg(string inner)
    {
        var depth = 0;
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (c is '(' or '[') depth++;
            else if (c is ')' or ']') depth--;
            else if (c == ',' && depth == 0)
            {
                return (inner[..i], inner[(i + 1)..]);
            }
        }
        return (null, null);
    }

    private static int FindMatchingParen(string source, int openParen)
    {
        var depth = 1;
        var i = openParen + 1;
        while (i < source.Length && depth > 0)
        {
            if (source[i] == '(') depth++;
            else if (source[i] == ')') depth--;
            if (depth > 0) i++;
        }
        return depth == 0 ? i : -1;
    }

    private static bool IsIdentChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
}