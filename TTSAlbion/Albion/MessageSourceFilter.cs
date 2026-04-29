namespace TTSAlbion.Albion;

/// <summary>
/// Flags that control which Albion chat events are processed by the TTS pipeline.
/// Using [Flags] allows combining sources without extra complexity.
/// </summary>
[Flags]
public enum MessageSourceFilter
{
    None,
    ChatMessage,   // EventCodes.ChatMessage — chat de zona/global
    ChatSay,    // EventCodes.ChatSay    — /say en mundo
    Both       = ChatMessage | ChatSay
}