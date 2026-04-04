using System.IO;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Converters;

public class BaseDirectoryPathResolver : IPathResolver
{
    private readonly string _basePath;

    public BaseDirectoryPathResolver(string? basePath = null)
    {
        _basePath = basePath ?? AppContext.BaseDirectory;
    }

    public string Resolve(string relativePath)
    {
        return Path.Combine(_basePath, relativePath);
    }
}