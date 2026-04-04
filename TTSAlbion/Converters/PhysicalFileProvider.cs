using System.IO;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Converters;

public class PhysicalFileProvider : IFileProvider
{
    public bool Exists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);
}