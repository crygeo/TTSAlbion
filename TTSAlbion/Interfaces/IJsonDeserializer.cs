namespace TTSAlbion.Interfaces;

public interface IJsonDeserializer
{
    T Deserialize<T>(string json);
    T FromFile<T>(string relativePath);
}