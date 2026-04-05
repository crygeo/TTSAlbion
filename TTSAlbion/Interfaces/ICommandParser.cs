namespace TTSAlbion.Interfaces;

public interface ICommandParser
{
    string CurrentPrefix { get; }
    void SetPrefix(string prefix);
    bool TryParse(string rawText, out string ttsPayload);
}