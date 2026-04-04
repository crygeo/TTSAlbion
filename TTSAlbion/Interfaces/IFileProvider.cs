namespace TTSAlbion.Interfaces;

public interface IFileProvider
{
    bool Exists(string path);
    string ReadAllText(string path);
}