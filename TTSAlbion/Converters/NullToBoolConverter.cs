namespace Utilidades.Converters;

// Namespace sugerido: Infrastructure.Converters
using System;
using System.Globalization;
using System.Windows.Data;

public sealed class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && s.Length > 0;
 
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}