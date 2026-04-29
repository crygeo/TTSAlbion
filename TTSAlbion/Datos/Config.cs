using TTSAlbion.Albion;
using TTSAlbion.Interfaces;
using TTSAlbion.Services.Audio;

namespace TTSAlbion.Datos;

/// <summary>
/// Inmutable snapshot of all persisted application settings.
/// Each logical section is grouped for readability; the struct stays flat
/// so JSON serialization requires zero custom converters.
///
/// Design: readonly struct + init-only setters enforces that Config objects
/// are never partially mutated — callers must build a new one via `with`.
/// </summary>
public readonly struct Config
{
    // ── App ─────────────────────────────────────────────────────────────────────
    public string Prefix     { get; init; }
    public string UserInGame       { get; init; }
    public ulong  UserIdDiscord      { get; init; }
    
    public MessageSourceFilter MessageSourceFilter { get; init; }
    
    public AudioSinkType AudioSinkType  { get; init; }
    
    //Traslate
    public bool UseTraslate { get; init; }
    public string SourceLang {  get; init; }
    public string TargetLang {  get; init; }
    public TranslatorOptions TranslatorOptions { get; init; }

    // ── Discord bot ──────────────────────────────────────────────────────────────
    public string BotToken        { get; init; }
    public string PathAlbion { get; init; }

}