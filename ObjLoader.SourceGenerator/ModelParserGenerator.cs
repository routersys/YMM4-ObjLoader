using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

#nullable enable

namespace ObjLoader.SourceGenerator
{
    [Generator]
    public class ModelParserGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var provider = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (s, _) => IsTargetSyntax(s),
                    transform: (ctx, _) => GetTargetSemantic(ctx))
                .Where(m => m != null);

            var compilation = context.CompilationProvider.Combine(provider.Collect());

            context.RegisterSourceOutput(compilation, (spc, source) => Execute(spc, source.Left, source.Right));
        }

        private static bool IsTargetSyntax(SyntaxNode node)
        {
            return node is ClassDeclarationSyntax c && c.AttributeLists.Count > 0;
        }

        private static ParserInfo? GetTargetSemantic(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

            if (symbol == null || symbol.IsAbstract || symbol.IsStatic) return null;

            var attributeData = symbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass != null &&
                                     (a.AttributeClass.Name == "ModelParserAttribute" ||
                                      a.AttributeClass.Name == "ModelParser"));

            if (attributeData == null) return null;

            var version = 1;
            var extensions = new List<string>();

            if (attributeData.ConstructorArguments.Length > 0)
            {
                if (attributeData.ConstructorArguments[0].Value is int v)
                {
                    version = v;
                }

                for (int i = 1; i < attributeData.ConstructorArguments.Length; i++)
                {
                    var arg = attributeData.ConstructorArguments[i];

                    if (arg.Kind == TypedConstantKind.Array && !arg.IsNull)
                    {
                        foreach (var val in arg.Values)
                        {
                            if (val.Value is string ext)
                            {
                                extensions.Add(ext);
                            }
                        }
                    }
                    else if (arg.Value is string s)
                    {
                        extensions.Add(s);
                    }
                }
            }

            var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "";

            return new ParserInfo
            {
                Namespace = ns,
                ClassName = symbol.Name,
                Version = version,
                Extensions = extensions
            };
        }

        private static void Execute(SourceProductionContext context, Compilation compilation, System.Collections.Immutable.ImmutableArray<ParserInfo?> parsers)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using ObjLoader.Core;");
            sb.AppendLine();
            sb.AppendLine("namespace ObjLoader.Parsers");
            sb.AppendLine("{");
            sb.AppendLine("    public partial class ObjModelLoader");
            sb.AppendLine("    {");
            sb.AppendLine("        private void LoadGeneratedParsers()");
            sb.AppendLine("        {");

            var validParsers = parsers.Where(p => p != null).OfType<ParserInfo>().ToList();
            if (validParsers.Count > 0)
            {
                sb.AppendLine("            IModelParser parser;");

                foreach (var parser in validParsers)
                {
                    var fullName = string.IsNullOrEmpty(parser.Namespace)
                        ? parser.ClassName
                        : $"{parser.Namespace}.{parser.ClassName}";

                    sb.AppendLine();
                    sb.AppendLine($"            parser = new {fullName}();");
                    sb.AppendLine($"            _parsers.Add(parser);");
                    sb.AppendLine($"            _parserVersions[typeof({fullName})] = {parser.Version};");

                    foreach (var ext in parser.Extensions)
                    {
                        sb.AppendLine($"            if (!_extensionMap.ContainsKey(\"{ext}\"))");
                        sb.AppendLine($"            {{");
                        sb.AppendLine($"                _extensionMap[\"{ext}\"] = new List<IModelParser>();");
                        sb.AppendLine($"            }}");
                        sb.AppendLine($"            _extensionMap[\"{ext}\"].Add(parser);");
                    }
                }
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource("ObjModelLoader.Generated.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private class ParserInfo
        {
            public string Namespace { get; set; } = "";
            public string ClassName { get; set; } = "";
            public int Version { get; set; }
            public List<string> Extensions { get; set; } = new List<string>();
        }
    }
}