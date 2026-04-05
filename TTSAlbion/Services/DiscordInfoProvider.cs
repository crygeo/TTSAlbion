using Discord.WebSocket;
using TTSAlbion.Interfaces;
using TTSAlbion.Services.Audio;
using TTSAlbion.Services.Tts;

namespace TTSAlbion.Services;

/// <summary>
/// Implementación real de IDiscordInfoProvider.
/// Resuelve nombres desde el DiscordSocketClient una sola vez y los cachea,
/// ya que cambiar de servidor/canal en runtime no está en scope.
/// </summary>
public sealed class DiscordInfoProvider : IDiscordInfoProvider
{
    public string GuildName   { get; }
    public string ChannelName { get; }

    public DiscordInfoProvider(DiscordSocketClient client, ulong guildId, ulong voiceChannelId)
    {
        var guild   = client.GetGuild(guildId);
        var channel = guild?.GetVoiceChannel(voiceChannelId);

        GuildName   = guild?.Name   ?? $"Guild {guildId}";
        ChannelName = channel?.Name ?? $"Canal {voiceChannelId}";
    }
}

/// <summary>
/// Comando TTS manual: sintetiza texto y lo envía a Discord.
/// Reutiliza el mismo pipeline que MessageService pero sin el filtro de usuario.
/// No duplica lógica — delega en las mismas abstracciones.
/// </summary>
public sealed class ManualTtsCommand : IManualTtsCommand, IDisposable
{
    private readonly ITtsEngine _ttsEngine;
    private readonly IAudioSink _audioSink;

    public ManualTtsCommand(ITtsEngine ttsEngine, IAudioSink audioSink)
    {
        _ttsEngine = ttsEngine ?? throw new ArgumentNullException(nameof(ttsEngine));
        _audioSink = audioSink ?? throw new ArgumentNullException(nameof(audioSink));
    }

    public async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var wav = await _ttsEngine.SynthesizeAsync(text, ct);
        if (wav.Length == 0) return;


        // Envío a sink tal cual, sin preocuparse de frames
        await _audioSink.SendAsync(wav, ct);
    }

    public void Dispose()
    {
        if (_ttsEngine is IDisposable d) d.Dispose();
    }
}