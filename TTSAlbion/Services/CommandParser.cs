// TTSAlbion/Services/CommandParser.cs

using TTSAlbion.Interfaces;

namespace TTSAlbion.Services;

public sealed class CommandParser : ICommandParser
{
    private readonly string _prefix;

    public CommandParser(string prefix = "!!")
    {
        _prefix = prefix;
    }

    public bool TryParse(string rawText, out string ttsPayload)
    {
        if (rawText.StartsWith(_prefix, StringComparison.Ordinal))
        {
            Console.WriteLine($"Command detected: {rawText}");
            ttsPayload = rawText[_prefix.Length..].Trim();
            return ttsPayload.Length > 0;
        }

        ttsPayload = string.Empty;
        return false;
    }
}