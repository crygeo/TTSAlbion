using System.IO;
using System.Text;
using Newtonsoft.Json;
using TTSAlbion.Datos;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Infrastructure;

/// <summary>
/// Persists <see cref="Config"/> as a JSON file on disk.
///
/// Design decisions:
/// - Uses Newtonsoft.Json (already a project dependency) for consistency.
/// - Write is atomic via a temp-file + rename pattern so a crash mid-write
///   never leaves a corrupted config file.
/// - SemaphoreSlim(1,1) serialises concurrent SaveAsync calls without
///   blocking the caller's thread (await vs lock).
/// - The file path is injected (not hardcoded) so the class is fully testable
///   with a temp path and works in both development and production layouts.
/// - Load is synchronous because it is called once at startup on the UI thread
///   before any async work begins — no deadlock risk, simpler call site.
/// </summary>
public sealed class JsonSettingsRepository : ISettingsRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented,
        // Preserves ulong precision (JSON number can lose precision for large IDs)
        // Newtonsoft handles ulong natively — no special converter needed.
    };

    /// <param name="filePath">
    /// Absolute path to the JSON config file.
    /// Recommended: pass <c>Path.Combine(AppContext.BaseDirectory, "Datos/config.json")</c>
    /// from the composition root so the path strategy lives in one place.
    /// </param>
    public JsonSettingsRepository(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    // ── ISettingsRepository ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Config? Load()
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            var json = File.ReadAllText(_filePath, Encoding.UTF8);

            return JsonConvert.DeserializeObject<Config>(json, SerializerSettings);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse settings file '{_filePath}'. " +
                $"Delete or fix the file and restart the application. ({ex.Message})", ex);
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Config config, CancellationToken ct = default)
    {
        var json = JsonConvert.SerializeObject(config, SerializerSettings);

        // Atomic write: write to temp → rename.
        // If the app crashes between the two operations the original file is intact.
        var tempPath = _filePath + ".tmp";

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Ensure directory exists (relevant on first run)
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, ct).ConfigureAwait(false);

            // File.Move with overwrite = true is atomic on the same volume (NTFS / ext4).
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _writeLock.Release();

            // Clean up temp file if the rename failed
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { /* best-effort */ }
        }
    }
}