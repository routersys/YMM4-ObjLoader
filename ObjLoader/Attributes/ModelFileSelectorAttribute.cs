using ObjLoader.Plugin.Parameters;
using ObjLoader.ViewModels;
using ObjLoader.Views;
using System.Reflection;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Attributes
{
    internal class ModelFileSelectorAttribute : PropertyEditorAttribute2
    {
        private readonly string _filter;
        private readonly string[] _extensions;

        public ModelFileSelectorAttribute(string filter, params string[] extensions)
        {
            _filter = filter;
            _extensions = extensions;
        }

        public override FrameworkElement Create()
        {
            return new ModelFileSelector();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is ModelFileSelector selector)
            {
                var property = itemProperties[0];
                ObjLoaderParameter? parameter = null;

                try
                {
                    var type = property.GetType();
                    var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                    foreach (var field in fields)
                    {
                        var value = field.GetValue(property);
                        if (value is ObjLoaderParameter p)
                        {
                            parameter = p;
                            break;
                        }
                    }
                }
                catch
                {
                }

                selector.DataContext = new ModelFileSelectorViewModel(property, parameter, _filter, _extensions);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is ModelFileSelector selector)
            {
                if (selector.DataContext is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                selector.DataContext = null;
            }
        }
    }
}