using System.Globalization;

namespace MobileApp.Converters
{
    /// <summary>
    /// Converts a boolean value to a color based on parameter format "TrueColor|FalseColor"
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not bool boolValue || parameter is not string colorParam)
                return Colors.Transparent;

            var colors = colorParam.Split('|');
            if (colors.Length != 2)
                return Colors.Transparent;

            var selectedColor = boolValue ? colors[0] : colors[1];
            return Color.FromArgb(selectedColor);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}