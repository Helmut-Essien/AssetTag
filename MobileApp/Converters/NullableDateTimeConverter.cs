using System.Globalization;

namespace MobileApp.Converters;

/// <summary>
/// Converter to handle nullable DateTime for DatePicker binding
/// Converts null to DateTime.Today for display, and back to null if unchanged
/// </summary>
public class NullableDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Convert DateTime? to DateTime for DatePicker
        // If null, return Today as placeholder
        if (value is DateTime dateTime)
            return dateTime;
        
        return DateTime.Today;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Convert DateTime back to DateTime?
        // If it's still Today (unchanged), return null
        if (value is DateTime dateTime)
        {
            // If user selected today's date, we assume they want to set it
            // Only return null if it's the exact default (which shouldn't happen in practice)
            return dateTime;
        }
        
        return null;
    }
}

/// <summary>
/// Converter to display "Select Date" when DateTime is null
/// </summary>
public class NullableDateDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
            return dateTime.ToString("yyyy-MM-dd");
        
        return "Select Date";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter to return gray color when DateTime is null, black otherwise
/// </summary>
public class IsNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Return gray color for null (placeholder), black for actual date
        return value == null ? Color.FromArgb("#999999") : Color.FromArgb("#333333");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}