using TTSAlbion.Datos;

namespace TTSAlbion.Interfaces;

/// <summary>
/// Abstracts persistence of application settings.
///
/// Design rationale:
/// - Separates the "what to store" (Config) from the "how to store it".
/// - Async write path prevents blocking the UI thread on disk I/O.
/// - Load returns a Result-style nullable so callers decide the fallback,
///   rather than throwing on a missing file (expected on first run).
/// - Keeping the interface narrow (Load / Save only) means implementations
///   can be swapped: JSON file → registry → remote config → in-memory (tests).
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Loads persisted settings.
    /// Returns <c>null</c> if the backing store does not exist yet (first run).
    /// Throws <see cref="InvalidOperationException"/> on parse failure so the
    /// caller can surface a meaningful error rather than silently using defaults.
    /// </summary>
    Config? Load();

    /// <summary>
    /// Persists <paramref name="config"/> to the backing store.
    /// The call is fire-and-forget safe: the caller may await or discard the task.
    /// </summary>
    Task SaveAsync(Config config, CancellationToken ct = default);
}