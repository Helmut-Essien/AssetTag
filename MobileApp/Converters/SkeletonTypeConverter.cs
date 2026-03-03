using System.Globalization;
using MobileApp.Controls;

namespace MobileApp.Converters;

public class SkeletonTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SkeletonType skeletonType && parameter is string targetTypeString)
        {
            if (Enum.TryParse<SkeletonType>(targetTypeString, out var parsedType))
            {
                return skeletonType == parsedType;
            }
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
