using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

#nullable enable

namespace ObjLoader.SourceGenerator
{
    [Generator]
    public class MenuSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;

            foreach (var classDeclaration in receiver.CandidateClasses)
            {
                var model = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = model.GetDeclaredSymbol(classDeclaration);

                if (classSymbol == null) continue;

                var menuItems = new List<MenuData>();

                foreach (var member in classSymbol.GetMembers())
                {
                    if (member is IPropertySymbol prop)
                    {
                        var attr = prop.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "MenuAttribute");
                        if (attr != null)
                        {
                            menuItems.Add(ParseAttribute(attr, prop.Name));
                        }
                    }
                    else if (member is IMethodSymbol method)
                    {
                        var attr = method.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "MenuAttribute");
                        if (attr != null)
                        {
                            menuItems.Add(ParseAttribute(attr, method.Name));
                        }
                    }
                }

                if (menuItems.Count > 0)
                {
                    var source = GeneratePartialClass(classSymbol, menuItems);
                    context.AddSource($"{classSymbol.Name}_Menu.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            }
        }

        private MenuData ParseAttribute(AttributeData attr, string memberName)
        {
            var data = new MenuData { MemberName = memberName };

            foreach (var namedArg in attr.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "Group": data.Group = namedArg.Value.Value?.ToString() ?? ""; break;
                    case "GroupNameKey": data.GroupNameKey = namedArg.Value.Value?.ToString() ?? ""; break;
                    case "GroupAcceleratorKey": data.GroupAcceleratorKey = namedArg.Value.Value?.ToString() ?? ""; break;
                    case "NameKey": data.NameKey = namedArg.Value.Value?.ToString() ?? ""; break;
                    case "ResourceType":
                        if (namedArg.Value.Value is ITypeSymbol typeSymbol)
                        {
                            data.ResourceType = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
                        }
                        else
                        {
                            data.ResourceType = namedArg.Value.Value?.ToString();
                        }
                        break;
                    case "Order": data.Order = (int)(namedArg.Value.Value ?? 0); break;
                    case "Icon": data.Icon = namedArg.Value.Value?.ToString() ?? ""; break;
                    case "IsCheckable": data.IsCheckable = (bool)(namedArg.Value.Value ?? false); break;
                    case "CheckPropertyName": data.CheckPropertyName = namedArg.Value.Value?.ToString() ?? ""; break;
                    case "IsSeparatorAfter": data.IsSeparatorAfter = (bool)(namedArg.Value.Value ?? false); break;
                    case "InputGestureText": data.InputGestureText = namedArg.Value.Value?.ToString() ?? ""; break;
                    case "AcceleratorKey": data.AcceleratorKey = namedArg.Value.Value?.ToString() ?? ""; break;
                }
            }
            return data;
        }

        private string GeneratePartialClass(INamedTypeSymbol classSymbol, List<MenuData> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.ObjectModel;");
            sb.AppendLine("using ObjLoader.ViewModels;");
            sb.AppendLine("using ObjLoader.ViewModels.Common;");
            sb.AppendLine("using ObjLoader.Localization;");
            sb.AppendLine("using System.Windows.Media;");
            sb.AppendLine("using System.Windows.Shapes;");
            sb.AppendLine("using System.Windows;");
            sb.AppendLine("using System.Windows.Input;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using YukkuriMovieMaker.Commons;");

            var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {classSymbol.Name}");
            sb.AppendLine("    {");

            var groupedItems = items.GroupBy(x => x.Group).OrderBy(g => g.Min(x => x.Order));
            var groupFields = new Dictionary<string, string>();

            foreach (var group in groupedItems)
            {
                var groupName = group.Key;
                if (!string.IsNullOrEmpty(groupName))
                {
                    var fieldName = $"_menuGroup_{SanitizeIdentifier(groupName)}";
                    groupFields[groupName] = fieldName;
                    sb.AppendLine($"        private MenuItemViewModel {fieldName};");
                }
            }

            sb.AppendLine("        partial void InitializeMenuItems()");
            sb.AppendLine("        {");
            sb.AppendLine("            this.MenuItems.Clear();");
            sb.AppendLine("            var groups = new System.Collections.Generic.Dictionary<string, MenuItemViewModel>();");

            foreach (var group in groupedItems)
            {
                var groupName = group.Key;

                if (!string.IsNullOrEmpty(groupName))
                {
                    var groupDef = group.FirstOrDefault(x => !string.IsNullOrEmpty(x.GroupNameKey)) ?? group.First();
                    var fieldName = groupFields[groupName];

                    sb.AppendLine($"            if (!groups.ContainsKey(\"{groupName}\"))");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                {fieldName} = new MenuItemViewModel();");

                    string headerSource;
                    if (!string.IsNullOrEmpty(groupDef.GroupNameKey) && !string.IsNullOrEmpty(groupDef.ResourceType))
                    {
                        headerSource = $"{groupDef.ResourceType}.{groupDef.GroupNameKey}";
                    }
                    else
                    {
                        headerSource = $"\"{groupName}\"";
                    }

                    if (!string.IsNullOrEmpty(groupDef.GroupAcceleratorKey))
                    {
                        sb.AppendLine($"                {fieldName}.Header = $\"{{{headerSource}}} ({groupDef.GroupAcceleratorKey})\";");
                    }
                    else
                    {
                        sb.AppendLine($"                {fieldName}.Header = {headerSource};");
                    }

                    sb.AppendLine($"                groups[\"{groupName}\"] = {fieldName};");
                    sb.AppendLine($"                this.MenuItems.Add({fieldName});");
                    sb.AppendLine("            }");
                }

                foreach (var item in group.OrderBy(x => x.Order))
                {
                    var varName = $"item_{item.MemberName}";
                    sb.AppendLine($"            var {varName} = new MenuItemViewModel();");

                    string headerSource;
                    if (!string.IsNullOrEmpty(item.NameKey) && !string.IsNullOrEmpty(item.ResourceType))
                    {
                        headerSource = $"{item.ResourceType}.{item.NameKey}";
                    }
                    else
                    {
                        headerSource = $"\"{item.NameKey}\"";
                    }

                    if (!string.IsNullOrEmpty(item.AcceleratorKey))
                    {
                        sb.AppendLine($"            {varName}.Header = $\"{{{headerSource}}} ({item.AcceleratorKey})\";");
                    }
                    else
                    {
                        sb.AppendLine($"            {varName}.Header = {headerSource};");
                    }

                    sb.AppendLine($"            {varName}.Command = this.{item.MemberName};");
                    sb.AppendLine($"            {varName}.IsCheckable = {item.IsCheckable.ToString().ToLower()};");
                    sb.AppendLine($"            {varName}.InputGestureText = \"{item.InputGestureText}\";");

                    if (!string.IsNullOrEmpty(item.Icon))
                    {
                        sb.AppendLine($"            try {{ {varName}.Icon = new Path {{ Data = Geometry.Parse(\"{item.Icon}\"), Fill = Brushes.Black, Stretch = Stretch.Uniform, Width = 16, Height = 16 }}; }} catch {{ {varName}.Icon = \"{item.Icon}\"; }}");
                    }

                    if (item.IsCheckable && !string.IsNullOrEmpty(item.CheckPropertyName))
                    {
                        sb.AppendLine($"            {varName}.SetCheckProperty(this, \"{item.CheckPropertyName}\");");
                        sb.AppendLine($"            this.PropertyChanged += (s, e) => {{ if (e.PropertyName == \"{item.CheckPropertyName}\") {varName}.UpdateCheckedState(); }};");
                    }

                    if (!string.IsNullOrEmpty(groupName))
                    {
                        sb.AppendLine($"            groups[\"{groupName}\"].Children.Add({varName});");
                    }
                    else
                    {
                        sb.AppendLine($"            this.MenuItems.Add({varName});");
                    }

                    if (item.IsSeparatorAfter)
                    {
                        if (!string.IsNullOrEmpty(groupName))
                        {
                            sb.AppendLine($"            groups[\"{groupName}\"].Children.Add(new MenuItemViewModel {{ IsSeparator = true }});");
                        }
                        else
                        {
                            sb.AppendLine("            this.MenuItems.Add(new MenuItemViewModel { IsSeparator = true });");
                        }
                    }
                }
            }
            sb.AppendLine("        }");

            sb.AppendLine("        public void RegisterMenuInputBindings(Window window)");
            sb.AppendLine("        {");

            foreach (var group in groupedItems)
            {
                var groupDef = group.FirstOrDefault(x => !string.IsNullOrEmpty(x.GroupNameKey)) ?? group.First();
                if (!string.IsNullOrEmpty(group.Key) && !string.IsNullOrEmpty(groupDef.GroupAcceleratorKey))
                {
                    var fieldName = groupFields[group.Key];
                    sb.AppendLine($"            try {{ window.InputBindings.Add(new KeyBinding(new ActionCommand(_ => true, _ => {{ if ({fieldName} != null) {fieldName}.IsSubmenuOpen = true; }}), (Key)Enum.Parse(typeof(Key), \"{groupDef.GroupAcceleratorKey}\"), ModifierKeys.Alt)); }} catch {{ }}");
                }
            }
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.AcceleratorKey))
                {
                    sb.AppendLine($"            try {{ window.InputBindings.Add(new KeyBinding(this.{item.MemberName}, (Key)Enum.Parse(typeof(Key), \"{item.AcceleratorKey}\"), ModifierKeys.Alt)); }} catch {{ }}");
                }
            }

            sb.AppendLine("            window.PreviewKeyDown += (s, e) =>");
            sb.AppendLine("            {");
            sb.AppendLine("                if (Keyboard.Modifiers != ModifierKeys.None && Keyboard.Modifiers != ModifierKeys.Shift) return;");

            foreach (var group in groupedItems)
            {
                var groupName = group.Key;
                if (!string.IsNullOrEmpty(groupName))
                {
                    var fieldName = groupFields[groupName];
                    sb.AppendLine($"                if ({fieldName} != null && {fieldName}.IsSubmenuOpen)");
                    sb.AppendLine("                {");

                    foreach (var item in group)
                    {
                        if (!string.IsNullOrEmpty(item.AcceleratorKey))
                        {
                            sb.AppendLine($"                    if (e.Key == (Key)Enum.Parse(typeof(Key), \"{item.AcceleratorKey}\")) {{ this.{item.MemberName}.Execute(null); {fieldName}.IsSubmenuOpen = false; e.Handled = true; return; }}");
                        }
                    }

                    sb.AppendLine("                }");
                }
            }

            sb.AppendLine("            };");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string SanitizeIdentifier(string name)
        {
            return Regex.Replace(name, @"\W", "");
        }

        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is ClassDeclarationSyntax classDeclaration &&
                    classDeclaration.AttributeLists.Count == 0)
                {
                    bool hasMenuAttribute = classDeclaration.Members.Any(m =>
                        m.AttributeLists.SelectMany(al => al.Attributes)
                        .Any(a => a.Name.ToString().Contains("Menu")));

                    if (hasMenuAttribute)
                    {
                        CandidateClasses.Add(classDeclaration);
                    }
                }
                else if (syntaxNode is ClassDeclarationSyntax cds)
                {
                    if (cds.Members.Any(m => m.AttributeLists.Count > 0))
                    {
                        CandidateClasses.Add(cds);
                    }
                }
            }
        }

        private class MenuData
        {
            public string MemberName { get; set; } = "";
            public string Group { get; set; } = "";
            public string GroupNameKey { get; set; } = "";
            public string GroupAcceleratorKey { get; set; } = "";
            public string NameKey { get; set; } = "";
            public string? ResourceType { get; set; }
            public int Order { get; set; }
            public string Icon { get; set; } = "";
            public bool IsCheckable { get; set; }
            public string CheckPropertyName { get; set; } = "";
            public bool IsSeparatorAfter { get; set; }
            public string InputGestureText { get; set; } = "";
            public string AcceleratorKey { get; set; } = "";
        }
    }
}