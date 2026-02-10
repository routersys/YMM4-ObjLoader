using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ObjLoader.SourceGenerator
{
    [Generator]
    public class MaterialSettingsGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver) return;

            var compilation = context.Compilation;

            foreach (var classDeclaration in receiver.CandidateClasses)
            {
                var model = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var symbol = model.GetDeclaredSymbol(classDeclaration);
                var classSymbol = symbol as INamedTypeSymbol;

                if (classSymbol == null) continue;

                bool hasAttribute = classSymbol.GetAttributes().Any(ad =>
                    ad.AttributeClass != null && ad.AttributeClass.Name == "MaterialGroupAttribute");

                if (!hasAttribute) continue;

                var source = GenerateSource(classSymbol);
                context.AddSource($"{classSymbol.Name}_MaterialSettings.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        private string GenerateSource(INamedTypeSymbol classSymbol)
        {
            var sb = new StringBuilder();
            var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
            var className = classSymbol.Name;

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Collections.ObjectModel;");
            sb.AppendLine("using ObjLoader.ViewModels;");
            sb.AppendLine("using ObjLoader.Localization;");
            sb.AppendLine("using System.Windows.Media;");

            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    internal static class {className}Factory");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static void CreateGroups(MaterialSettingsViewModel vm, {className} target, Action onUpdate)");
            sb.AppendLine("        {");

            var groups = new List<(string Id, string TitleKey, int Order)>();
            foreach (var attr in classSymbol.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "MaterialGroupAttribute")
                {
                    var id = (string)attr.ConstructorArguments[0].Value;
                    var titleKey = (string)attr.ConstructorArguments[1].Value;
                    var order = (int)attr.ConstructorArguments[2].Value;
                    groups.Add((id, titleKey, order));
                }
            }

            groups.Sort((a, b) => a.Order.CompareTo(b.Order));

            var properties = new List<(IPropertySymbol Symbol, string GroupId, AttributeData Attribute, int Order)>();
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IPropertySymbol prop)
                {
                    foreach (var attr in prop.GetAttributes())
                    {
                        if (attr.AttributeClass != null &&
                           (attr.AttributeClass.Name == "MaterialRangeAttribute" || attr.AttributeClass.Name == "MaterialColorAttribute"))
                        {
                            var groupId = (string)attr.ConstructorArguments[0].Value;
                            int order = 0;

                            if (attr.AttributeClass.Name == "MaterialRangeAttribute")
                            {
                                order = (int)attr.ConstructorArguments[5].Value;
                            }
                            else if (attr.AttributeClass.Name == "MaterialColorAttribute")
                            {
                                order = (int)attr.ConstructorArguments[2].Value;
                            }

                            properties.Add((prop, groupId, attr, order));
                        }
                    }
                }
            }

            foreach (var group in groups)
            {
                sb.AppendLine($"            var group_{group.Id} = new MaterialGroupViewModel(\"{group.Id}\", \"{group.TitleKey}\", onUpdate);");

                var groupProps = properties.Where(p => p.GroupId == group.Id).OrderBy(p => p.Order);

                foreach (var prop in groupProps)
                {
                    var attr = prop.Attribute;
                    var propName = prop.Symbol.Name;
                    var labelKey = (string)attr.ConstructorArguments[1].Value;

                    if (attr.AttributeClass.Name == "MaterialRangeAttribute")
                    {
                        var min = (double)attr.ConstructorArguments[2].Value;
                        var max = (double)attr.ConstructorArguments[3].Value;
                        var step = (double)attr.ConstructorArguments[4].Value;

                        sb.AppendLine($"            group_{group.Id}.Items.Add(new MaterialRangeItemViewModel(");
                        sb.AppendLine($"                \"{labelKey}\",");
                        sb.AppendLine($"                () => target.{propName},");
                        sb.AppendLine($"                v => target.{propName} = v,");
                        sb.AppendLine($"                {min}, {max}, {step},");
                        sb.AppendLine($"                onUpdate));");
                    }
                    else if (attr.AttributeClass.Name == "MaterialColorAttribute")
                    {
                        sb.AppendLine($"            group_{group.Id}.Items.Add(new MaterialColorItemViewModel(");
                        sb.AppendLine($"                \"{labelKey}\",");
                        sb.AppendLine($"                () => target.{propName},");
                        sb.AppendLine($"                v => target.{propName} = v,");
                        sb.AppendLine($"                onUpdate));");
                    }
                }

                sb.AppendLine($"            vm.Groups.Add(group_{group.Id});");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        class SyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax &&
                    classDeclarationSyntax.AttributeLists.Count > 0)
                {
                    CandidateClasses.Add(classDeclarationSyntax);
                }
            }
        }
    }
}