namespace TTSAlbion.Datos;

public readonly struct Config
{
    public string Token { get; init; }
    public string Prefix { get; init; } 
    public ulong GuildId { get; init; }
    public ulong VoiceChannelId { get; init; }
    public ulong ChannelId { get; init; }
    
    public string PathAlbion { get; init; }
    
}