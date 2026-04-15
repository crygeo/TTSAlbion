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
    public string User       { get; init; }
    public ulong  UserId      { get; init; }
    
    public AudioSinkType AudioSinkType  { get; init; }
    
    

    // ── Discord bot ──────────────────────────────────────────────────────────────
    public string BotToken        { get; init; }
    public string PathAlbion { get; init; }

}