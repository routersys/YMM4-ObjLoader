using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ObjLoader.Converters
{
    public class StringVisibilityConverter : IValueConverter
    {
        public static StringVisibilityConverter Instance = new StringVisibilityConverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}