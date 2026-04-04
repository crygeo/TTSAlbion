using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Utilidades.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            var isReversed = parameter as string == "Invert"; // Si el parámetro es "Invert", invierte la lógica
            return boolValue ^ isReversed ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            var isReversed = parameter as string == "Invert";
            return (visibility == Visibility.Visible) ^ isReversed;
        }

        return false;
    }
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    private readonly BooleanToVisibilityConverter _baseConverter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return _baseConverter.Convert(value, targetType, "Invert", culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return _baseConverter.ConvertBack(value, targetType, "Invert", culture);
    }
}