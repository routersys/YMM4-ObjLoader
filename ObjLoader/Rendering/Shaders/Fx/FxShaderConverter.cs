using ObjLoader.Rendering.Shaders.Interfaces;
using System.Text.RegularExpressions;

namespace ObjLoader.Rendering.Shaders.Fx;

public sealed class FxShaderConverter : IShaderConverter
{
    private static readonly Regex ContainsVsPattern = new(
        @"\bVS\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex ContainsPsPattern = new(
        @"\bPS\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex MultipleBlankLinesPattern = new(
        @"\n(\s*\n){2,}",
        RegexOptions.Compiled);

    private static readonly FxPropertyCollector PropertyCollector = new();
    private static readonly FxSemanticRemapper SemanticRemapper = new();
    private static readonly FxSamplerCallConverter SamplerCallConverter = new();
    private static readonly FxShaderAssembler Assembler = new();

    public string Convert(string sourceCode)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);

        var properties = PropertyCollector.Collect(sourceCode);
        var remapped = SemanticRemapper.Remap(sourceCode, properties);
        var callsFixed = SamplerCallConverter.Convert(remapped, properties);
        var entryRenamed = RenameEntryPoints(callsFixed, properties);
        var cleaned = CollapseBlankLines(entryRenamed);

        if (properties.IsPostEffect || !ContainsVsPattern.IsMatch(cleaned) || !ContainsPsPattern.IsMatch(cleaned))
        {
            return string.Empty;
        }

        return Assembler.Assemble(cleaned, properties);
    }

    private static string RenameEntryPoints(string source, FxCollectedProperties properties)
    {
        if (!string.IsNullOrEmpty(properties.VsEntryPoint))
        {
            var pattern = new Regex($@"\b{Regex.Escape(properties.VsEntryPoint)}\s*\(");
            source = pattern.Replace(source, "VS(");
        }

        if (!string.IsNullOrEmpty(properties.PsEntryPoint))
        {
            var pattern = new Regex($@"\b{Regex.Escape(properties.PsEntryPoint)}\s*\(");
            source = pattern.Replace(source, "PS(");
        }

        return source;
    }

    private static string CollapseBlankLines(string source) =>
        MultipleBlankLinesPattern.Replace(source, "\n\n");
}