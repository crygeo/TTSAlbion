// TTSAlbion/Services/ICommandParser.cs
namespace TTSAlbion.Interfaces;

public interface ICommandParser
{
    bool TryParse(string rawText, out string ttsPayload);
}