using Discord;
using Discord.WebSocket;
using NAudio.Wave;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Services.Audio;

/// <summary>
/// Default implementation of <see cref="IAudioSinkFactory"/>.
/// Each <see cref="Create"/> call returns a fresh, independent sink instance.
/// Discord bot sinks manage their own <see cref="DiscordSocketClient"/> lifecycle.
/// </summary>
public sealed class DefaultAudioSinkFactory : IAudioSinkFactory
{
    public static DiscordSocketClient? Client;
    private AudioSinkType _lastCreatedType;

    public async Task<IAudioSink> Create(AudioSinkType type, DiscordBotConfig? botConfig = null)
    {
        return type switch
        {
            AudioSinkType.Local => new LocalAudioSink(),
            AudioSinkType.VirtualMic => new VirtualMicAudioSink(),
            AudioSinkType.DiscordBot => await CreateBotSink(botConfig),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private static async Task<IAudioSink> CreateBotSink(DiscordBotConfig? config)
    {
        if (config is null)
            throw new ArgumentNullException(nameof(config),
                "DiscordBotConfig is required when creating a DiscordBot audio sink.");

        if (Client is null)
        {
            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                EnableVoiceDaveEncryption = true
            });
        }

        var readyTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Client.Ready += () =>
        {
            readyTcs.TrySetResult();
            return Task.CompletedTask;
        };

        await Client.LoginAsync(TokenType.Bot, config.Token).ConfigureAwait(false);
        await Client.StartAsync().ConfigureAwait(false);

        await Task.WhenAny(readyTcs.Task, Task.Delay(TimeSpan.FromSeconds(15)))
            .ConfigureAwait(false);

        // The sink owns the client and is responsible for connecting.
        // Connection is deferred to first SendAsync to avoid blocking startup.
        return new DiscordAudioSink(Client, config.GuildId, config.VoiceChannelId);
    }
}