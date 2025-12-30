using System.Windows;
using System.Windows.Controls;
using ObjLoader.Infrastructure;

namespace ObjLoader.Utilities
{
    public class SettingTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? TextTemplate { get; set; }
        public DataTemplate? BoolTemplate { get; set; }
        public DataTemplate? RangeTemplate { get; set; }
        public DataTemplate? EnumTemplate { get; set; }
        public DataTemplate? ColorTemplate { get; set; }
        public DataTemplate? FileTemplate { get; set; }
        public DataTemplate? ButtonTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            return item switch
            {
                TextSettingViewModel => TextTemplate,
                BoolSettingViewModel => BoolTemplate,
                RangeSettingViewModel => RangeTemplate,
                EnumSettingViewModel => EnumTemplate,
                ColorSettingViewModel => ColorTemplate,
                FilePathSettingViewModel => FileTemplate,
                ButtonSettingViewModel => ButtonTemplate,
                _ => base.SelectTemplate(item, container)
            };
        }
    }
}