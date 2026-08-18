using System.Globalization;
using MobileApp.Converters;
using MobileApp.Controls;
using Xunit;

namespace MobileApp.Tests.Converters;

public sealed class ConverterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void InvertedBoolConverter_FlipsValue(bool input, bool expected)
    {
        var converter = new InvertedBoolConverter();
        Assert.Equal(expected, converter.Convert(input, typeof(bool), null, Culture));
        Assert.Equal(input, converter.ConvertBack(expected, typeof(bool), null, Culture));
    }

    [Fact]
    public void StringToBoolConverter_TrueWhenNonEmpty()
    {
        var converter = new StringToBoolConverter();
        Assert.Equal(true, converter.Convert("hello", typeof(bool), null, Culture));
        Assert.Equal(false, converter.Convert("", typeof(bool), null, Culture));
        Assert.Equal(false, converter.Convert(null, typeof(bool), null, Culture));
    }

    [Fact]
    public void BoolToColorConverter_SelectsColorFromParameter()
    {
        var converter = new BoolToColorConverter();
        var trueColor = (Color)converter.Convert(true, typeof(Color), "#005A9C|#E0E0E0", Culture)!;
        var falseColor = (Color)converter.Convert(false, typeof(Color), "#005A9C|#E0E0E0", Culture)!;

        Assert.Equal(Color.FromArgb("#005A9C"), trueColor);
        Assert.Equal(Color.FromArgb("#E0E0E0"), falseColor);
    }

    [Fact]
    public void NullableDateTimeConverter_NullBecomesToday()
    {
        var converter = new NullableDateTimeConverter();
        var result = (DateTime)converter.Convert(null, typeof(DateTime), null, Culture)!;
        Assert.Equal(DateTime.Today, result.Date);
    }

    [Fact]
    public void NullableDateDisplayConverter_NullShowsPlaceholder()
    {
        var converter = new NullableDateDisplayConverter();
        Assert.Equal("Select Date", converter.Convert(null, typeof(string), null, Culture));
        Assert.Equal("2026-08-17", converter.Convert(new DateTime(2026, 8, 17), typeof(string), null, Culture));
    }

    [Fact]
    public void SkeletonTypeConverter_MatchesEnumName()
    {
        var converter = new SkeletonTypeConverter();
        Assert.Equal(true, converter.Convert(SkeletonType.AssetItem, typeof(bool), "AssetItem", Culture));
        Assert.Equal(false, converter.Convert(SkeletonType.AssetItem, typeof(bool), "Card", Culture));
    }
}
