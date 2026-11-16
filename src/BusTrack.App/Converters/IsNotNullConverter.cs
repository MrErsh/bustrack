using System.Globalization;

namespace BusTrack.App.Converters;

/// <summary>Видимость элемента: показываем, когда значение не null (и не пустая строка).</summary>
public sealed class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && value is not string { Length: 0 };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}