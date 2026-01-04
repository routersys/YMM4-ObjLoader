using System.Globalization;
using System.Windows.Data;

namespace ObjLoader.Converters
{
    public class StringToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    return 0.0;
                }

                if (double.TryParse(s, out double result))
                {
                    return result;
                }
            }
            return Binding.DoNothing;
        }
    }
}