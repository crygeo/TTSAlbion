namespace LibAlbionProtocol.Interfaces;

public interface IValueConverter<out T>
{
    T? Convert(object rawValue);
}