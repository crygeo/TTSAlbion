using TTSAlbion.Interfaces;

namespace TTSAlbion.Services;

/// <summary>
/// Detects and strips a configurable command prefix from raw chat text.
///
/// Design notes:
/// - Prefix is stored in a volatile field so reads on non-UI threads
///   always see the latest value written by the UI thread.
///   (Full thread-safety would require Interlocked/lock; volatile suffices
///   here because string assignment is atomic on CLR and the worst case
///   is one stale read, not corruption.)
/// - Intentionally kept stateless beyond the prefix so it is trivially testable.
/// </summary>
public sealed class CommandParser : ICommandParser
{
    private volatile string _prefix;

    public CommandParser(string prefix = "!!")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        _prefix = prefix;
    }

    /// <summary>
    /// Updates the active prefix. Safe to call from the UI thread at any time.
    /// An empty or whitespace-only prefix is rejected to prevent matching everything.
    /// </summary>
    public void SetPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix must contain at least one non-whitespace character.", nameof(prefix));

        _prefix = prefix;
    }

    public string CurrentPrefix => _prefix;

    public bool TryParse(string rawText, out string ttsPayload)
    {
        var prefix = _prefix; // snapshot — avoids TOCTOU on volatile read

        if (rawText.StartsWith(prefix, StringComparison.Ordinal))
        {
            ttsPayload = rawText[prefix.Length..].Trim();
            return ttsPayload.Length > 0;
        }

        ttsPayload = string.Empty;
        return false;
    }
}