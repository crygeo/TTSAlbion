using System.IO;
using System.Text.Json;
using Newtonsoft.Json;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Converters;

public class NewtonsoftJsonDeserializer : IJsonDeserializer
{
    private readonly IFileProvider _fileProvider;
    private readonly IPathResolver _pathResolver;

    public NewtonsoftJsonDeserializer(
        IFileProvider fileProvider,
        IPathResolver pathResolver)
    {
        _fileProvider = fileProvider;
        _pathResolver = pathResolver;
    }

    public T Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON vacío.");

        return JsonConvert.DeserializeObject<T>(json)
               ?? throw new InvalidOperationException("Error al deserializar.");
    }

    public T FromFile<T>(string relativePath)
    {
        var fullPath = _pathResolver.Resolve(relativePath);

        if (!_fileProvider.Exists(fullPath))
            throw new FileNotFoundException("Archivo no encontrado.", fullPath);

        var json = _fileProvider.ReadAllText(fullPath);

        return Deserialize<T>(json);
    }
}