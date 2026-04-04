namespace TTSAlbion.Interfaces;

/// <summary>
/// Ejecuta TTS de forma directa, sin filtro de usuario.
/// Abstrae el pipeline TTS → Discord del ViewModel.
/// </summary>
public interface IManualTtsCommand
{
    Task SpeakAsync(string text, CancellationToken ct = default);
}

/// <summary>
/// Provee nombres del servidor y canal de voz de Discord.
/// Permite al ViewModel mostrar info de Discord sin acoplarse a Discord.Net.
/// </summary>
public interface IDiscordInfoProvider
{
    string GuildName { get; }
    string ChannelName { get; }
}