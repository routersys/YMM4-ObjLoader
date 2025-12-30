using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Globalization;

namespace ObjLoader.SourceGenerator
{
    [Generator]
    public class SettingsGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => s is ClassDeclarationSyntax,
                    transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
                .Where(static m => m != null);

            var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Left, source.Right, spc));
        }

        private static INamedTypeSymbol GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;
            return context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) as INamedTypeSymbol;
        }

        private static void Execute(Compilation compilation, ImmutableArray<INamedTypeSymbol> classes, SourceProductionContext context)
        {
            var settingsItemAttrSymbol = compilation.GetTypeByMetadataName("ObjLoader.Infrastructure.SettingItemAttribute");
            var settingButtonAttrSymbol = compilation.GetTypeByMetadataName("ObjLoader.Infrastructure.SettingButtonAttribute");
            var settingGroupAttrSymbol = compilation.GetTypeByMetadataName("ObjLoader.Infrastructure.SettingGroupAttribute");

            if (settingsItemAttrSymbol == null || settingButtonAttrSymbol == null) return;

            var distinctClasses = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var typeSymbol in classes)
            {
                if (typeSymbol == null) continue;
                if (HasSettings(typeSymbol, settingsItemAttrSymbol, settingButtonAttrSymbol, settingGroupAttrSymbol))
                {
                    distinctClasses.Add(typeSymbol);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using ObjLoader.ViewModels;");
            sb.AppendLine("using ObjLoader.Infrastructure;");
            sb.AppendLine("using YukkuriMovieMaker.Commons;");
            sb.AppendLine("using System.ComponentModel;");
            sb.AppendLine("using System.Windows.Data;");
            sb.AppendLine();
            sb.AppendLine("namespace ObjLoader.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class SettingsInitializer");
            sb.AppendLine("    {");
            sb.AppendLine("        internal static bool TryInitialize(object target, SettingWindowViewModel vm)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (target == null) return false;");
            sb.AppendLine("            switch (target)");
            sb.AppendLine("            {");

            foreach (var type in distinctClasses)
            {
                var fullTypeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var methodName = GetSafeMethodName(type);
                sb.AppendLine($"                case {fullTypeName} t:");
                sb.AppendLine($"                    {methodName}(t, vm);");
                sb.AppendLine("                    return true;");
            }

            sb.AppendLine("                default:");
            sb.AppendLine("                    return false;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            foreach (var type in distinctClasses)
            {
                GenerateInitMethod(sb, type, settingsItemAttrSymbol, settingButtonAttrSymbol, settingGroupAttrSymbol);
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource("SettingsInitializer.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static string GetSafeMethodName(INamedTypeSymbol type)
        {
            var name = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)
                .Replace(".", "_")
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace(" ", "");
            return $"Initialize_{name}";
        }

        private static bool HasSettings(INamedTypeSymbol type, INamedTypeSymbol itemAttr, INamedTypeSymbol btnAttr, INamedTypeSymbol groupAttr)
        {
            foreach (var member in type.GetMembers())
            {
                foreach (var attr in member.GetAttributes())
                {
                    if (attr.AttributeClass == null) continue;

                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, btnAttr) ||
                        SymbolEqualityComparer.Default.Equals(attr.AttributeClass, groupAttr) ||
                        InheritsFrom(attr.AttributeClass, itemAttr))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            var current = type;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
                current = current.BaseType;
            }
            return false;
        }

        private static void GenerateInitMethod(StringBuilder sb, INamedTypeSymbol type, INamedTypeSymbol itemAttrBase, INamedTypeSymbol btnAttr, INamedTypeSymbol groupAttr)
        {
            var fullTypeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var methodName = GetSafeMethodName(type);
            sb.AppendLine($"        private static void {methodName}({fullTypeName} target, SettingWindowViewModel vm)");
            sb.AppendLine("        {");
            sb.AppendLine("            var groupDict = new Dictionary<string, SettingGroupViewModel>();");

            var members = type.GetMembers();
            foreach (var member in members)
            {
                var attrs = member.GetAttributes();
                var memberGroupAttr = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, groupAttr));

                if (memberGroupAttr != null && memberGroupAttr.ConstructorArguments.Length >= 2)
                {
                    var id = memberGroupAttr.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
                    var title = memberGroupAttr.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
                    var order = GetNamedArgument(memberGroupAttr, "Order", 0);

                    var iconArg = memberGroupAttr.NamedArguments.FirstOrDefault(na => na.Key == "Icon");
                    var icon = iconArg.Key != null ? FormatTypedConstant(iconArg.Value) : "\"Geometry\"";

                    sb.AppendLine($"            if (!groupDict.ContainsKey(\"{id}\"))");
                    sb.AppendLine($"                groupDict[\"{id}\"] = new SettingGroupViewModel(\"{id}\", \"{title}\", {order}, {icon});");
                }
            }

            foreach (var member in members)
            {
                if (member is IPropertySymbol prop)
                {
                    var itemAttribute = prop.GetAttributes().FirstOrDefault(a => InheritsFrom(a.AttributeClass, itemAttrBase));
                    if (itemAttribute != null && itemAttribute.ConstructorArguments.Length > 0)
                    {
                        var groupId = itemAttribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
                        sb.AppendLine($"            if (!groupDict.ContainsKey(\"{groupId}\"))");
                        sb.AppendLine($"                groupDict[\"{groupId}\"] = new SettingGroupViewModel(\"{groupId}\", \"{groupId}\", 0, \"Geometry\");");

                        var vmType = GetViewModelType(itemAttribute.AttributeClass.Name);
                        var attrCreation = GenerateAttributeCreation(itemAttribute);

                        sb.AppendLine($"            {{");
                        sb.AppendLine($"                var propInfo = typeof({fullTypeName}).GetProperty(\"{prop.Name}\", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);");
                        sb.AppendLine($"                if (propInfo != null)");
                        sb.AppendLine($"                {{");
                        sb.AppendLine($"                    var itemVm = new {vmType}(target, propInfo, {attrCreation});");
                        sb.AppendLine($"                    itemVm.PropertyChanged += (s, e) => {{ if (s is SettingItemViewModelBase item) vm.Description = item.Description; }};");
                        sb.AppendLine($"                    groupDict[\"{groupId}\"].Items.Add(itemVm);");
                        sb.AppendLine($"                }}");
                        sb.AppendLine($"            }}");
                    }
                }
            }

            foreach (var member in members)
            {
                if (member is IMethodSymbol method)
                {
                    var btnAttribute = method.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, btnAttr));
                    if (btnAttribute != null)
                    {
                        var attrCreation = GenerateAttributeCreation(btnAttribute);
                        var placement = (int)GetNamedArgument(btnAttribute, "Placement", 0);
                        var groupId = GetNamedArgument(btnAttribute, "GroupId", string.Empty).ToString().Trim('"');
                        var btnType = (int)GetNamedArgument(btnAttribute, "Type", 0);

                        string actionLambda = "w => { }";
                        if (btnType == 1 || btnType == 2 || btnType == 3)
                        {
                            actionLambda = "w => w?.Close()";
                        }

                        sb.AppendLine($"            {{");
                        sb.AppendLine($"                var methodInfo = typeof({fullTypeName}).GetMethod(\"{method.Name}\", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);");
                        sb.AppendLine($"                if (methodInfo != null)");
                        sb.AppendLine($"                {{");
                        sb.AppendLine($"                    var btnVm = new ButtonSettingViewModel(target, methodInfo, {attrCreation}, {actionLambda});");
                        sb.AppendLine($"                    btnVm.PropertyChanged += (s, e) => vm.Description = btnVm.Description;");

                        if (placement == 0)
                        {
                            if (!string.IsNullOrEmpty(groupId))
                            {
                                sb.AppendLine($"                    if (groupDict.TryGetValue(\"{groupId}\", out var group)) group.Items.Add(btnVm);");
                            }
                        }
                        else if (placement == 1)
                        {
                            sb.AppendLine($"                    vm.LeftButtons.Add(btnVm);");
                        }
                        else if (placement == 2)
                        {
                            sb.AppendLine($"                    vm.RightButtons.Add(btnVm);");
                        }
                        sb.AppendLine($"                }}");
                        sb.AppendLine($"            }}");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("            var sortedGroups = groupDict.Values.ToList();");
            sb.AppendLine("            sortedGroups.Sort();");
            sb.AppendLine("            foreach (var group in sortedGroups)");
            sb.AppendLine("            {");
            sb.AppendLine("                var view = CollectionViewSource.GetDefaultView(group.Items);");
            sb.AppendLine("                if (view != null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    view.SortDescriptions.Add(new SortDescription(nameof(SettingItemViewModelBase.Order), ListSortDirection.Ascending));");
            sb.AppendLine("                }");
            sb.AppendLine("                vm.Groups.Add(group);");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            SortButtons(vm.LeftButtons);");
            sb.AppendLine("            SortButtons(vm.RightButtons);");
            sb.AppendLine("            if (vm.Groups.Count > 0) vm.SelectedGroup = vm.Groups[0];");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static void SortButtons(System.Collections.ObjectModel.ObservableCollection<ButtonSettingViewModel> collection)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (collection == null) return;");
            sb.AppendLine("            var list = collection.ToList();");
            sb.AppendLine("            list.Sort((a, b) => a.Order.CompareTo(b.Order));");
            sb.AppendLine("            collection.Clear();");
            sb.AppendLine("            foreach (var item in list) collection.Add(item);");
            sb.AppendLine("        }");
        }

        private static string GetViewModelType(string attributeName)
        {
            return attributeName switch
            {
                "TextSettingAttribute" => "TextSettingViewModel",
                "BoolSettingAttribute" => "BoolSettingViewModel",
                "RangeSettingAttribute" => "RangeSettingViewModel",
                "EnumSettingAttribute" => "EnumSettingViewModel",
                "ColorSettingAttribute" => "ColorSettingViewModel",
                "FilePathSettingAttribute" => "FilePathSettingViewModel",
                _ => "PropertySettingViewModel"
            };
        }

        private static string GenerateAttributeCreation(AttributeData attr)
        {
            var sb = new StringBuilder();
            sb.Append($"new {attr.AttributeClass.ToDisplayString()}(");

            var args = attr.ConstructorArguments.Select(FormatTypedConstant).ToArray();
            sb.Append(string.Join(", ", args));
            sb.Append(")");

            if (attr.NamedArguments.Length > 0)
            {
                sb.Append(" { ");
                var namedArgs = attr.NamedArguments.Select(na => $"{na.Key} = {FormatTypedConstant(na.Value)}");
                sb.Append(string.Join(", ", namedArgs));
                sb.Append(" }");
            }

            return sb.ToString();
        }

        private static string FormatTypedConstant(TypedConstant val)
        {
            if (val.IsNull) return "null";
            if (val.Kind == TypedConstantKind.Type)
            {
                if (val.Value is ITypeSymbol typeSymbol)
                {
                    return $"typeof({typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
                }
                return "null";
            }
            if (val.Kind == TypedConstantKind.Array) return "null";
            if (val.Kind == TypedConstantKind.Enum) return $"({val.Type.ToDisplayString()}){val.Value}";
            if (val.Value is string s) return $"\"{s}\"";
            if (val.Value is bool b) return b ? "true" : "false";
            if (val.Value is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
            return val.Value?.ToString() ?? "null";
        }

        private static object GetNamedArgument(AttributeData attr, string name, object defaultValue)
        {
            var arg = attr.NamedArguments.FirstOrDefault(na => na.Key == name);
            if (arg.Key != null) return arg.Value.Value;
            return defaultValue;
        }
    }
}