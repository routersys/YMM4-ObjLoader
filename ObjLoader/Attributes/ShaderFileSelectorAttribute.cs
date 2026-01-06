using System.Reflection;
using System.Windows;
using ObjLoader.Localization;
using ObjLoader.ViewModels;
using ObjLoader.Views;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Attributes
{
    public class ShaderFileSelectorAttribute : PropertyEditorAttribute2
    {
        private readonly string[] _args;

        public ShaderFileSelectorAttribute(params string[] args)
        {
            _args = args;
        }

        public override FrameworkElement Create()
        {
            return new ShaderFileSelector();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is ShaderFileSelector selector && itemProperties.Length > 0)
            {
                var filters = new List<string>();
                var allExtensions = new List<string>();

                string? currentLabel = null;
                var currentExtensions = new List<string>();

                foreach (var arg in _args)
                {
                    if (arg.StartsWith("."))
                    {
                        currentExtensions.Add(arg);
                        allExtensions.Add(arg);
                    }
                    else
                    {
                        if (currentExtensions.Count > 0)
                        {
                            AddFilter(filters, currentLabel, currentExtensions);
                            currentExtensions.Clear();
                        }
                        currentLabel = arg;
                    }
                }

                if (currentExtensions.Count > 0)
                {
                    AddFilter(filters, currentLabel, currentExtensions);
                }

                filters.Add("All Files|*.*");
                var filterString = string.Join("|", filters);

                selector.DataContext = new ShaderFileSelectorViewModel(itemProperties[0], filterString, allExtensions);
            }
        }

        private void AddFilter(List<string> filters, string? label, List<string> extensions)
        {
            var displayLabel = label ?? "Files";

            if (label != null)
            {
                var property = typeof(Texts).GetProperty(label, BindingFlags.Static | BindingFlags.Public);
                if (property != null && property.GetValue(null) is string localized)
                {
                    displayLabel = localized;
                }
            }

            var pattern = string.Join(";", extensions.Select(e => "*" + e));
            filters.Add($"{displayLabel}|{pattern}");
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is ShaderFileSelector selector)
            {
                selector.DataContext = null;
            }
        }
    }
}