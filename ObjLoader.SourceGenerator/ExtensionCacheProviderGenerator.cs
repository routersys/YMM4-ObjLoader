using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ObjLoader.SourceGenerator
{
    [Generator]
    public class ExtensionCacheProviderGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
                return;

            var sb = new StringBuilder();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using ObjLoader.Cache;");
            sb.AppendLine("");
            sb.AppendLine("namespace ObjLoader.Cache.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public static class ExtensionCacheProviderRegistry");
            sb.AppendLine("    {");
            sb.AppendLine("        public static List<IExtensionCacheProvider> GetProviders()");
            sb.AppendLine("        {");
            sb.AppendLine("            return new List<IExtensionCacheProvider>");
            sb.AppendLine("            {");

            foreach (var classSymbol in receiver.Classes)
            {
                sb.AppendLine($"                new {classSymbol.ToDisplayString()}(),");
            }

            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource("ExtensionCacheProviderRegistry.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        class SyntaxReceiver : ISyntaxContextReceiver
        {
            public List<INamedTypeSymbol> Classes { get; } = new List<INamedTypeSymbol>();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (context.Node is ClassDeclarationSyntax classDeclarationSyntax && classDeclarationSyntax.AttributeLists.Count > 0)
                {
                    var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) as INamedTypeSymbol;
                    if (symbol != null)
                    {
                        if (symbol.GetAttributes().Any(ad => ad.AttributeClass?.ToDisplayString() == "ObjLoader.Attributes.ExtensionCacheProviderAttribute"))
                        {
                            Classes.Add(symbol);
                        }
                    }
                }
            }
        }
    }
}