namespace TTSAlbion.Albion.Models.Converters;

public interface IValueConverter<out T>
{
    T? Convert(object rawValue);
}