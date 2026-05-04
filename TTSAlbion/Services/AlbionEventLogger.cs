using LibAlbionProtocol.Models;
using LibAlbionProtocol.PacketModels;
using LibAlbionRouting.Dispatching;

namespace TTSAlbion.Services;

public sealed class AlbionEventLogger
{
    public AlbionEventLogger(IEventDispatcher dispatcher)
    {
        dispatcher.Subscribe<ChatSayModel>(EventCodes.ChatSay, async model =>
        {
            Console.WriteLine($"[ChatLog] {model.User}: {model.Message}");
            await Task.CompletedTask;
        });

        dispatcher.Subscribe<CharacterStatsModel>(EventCodes.CharacterStats, async model =>
        {
            Console.WriteLine($"[StatsLog] {model.NameUser}");
            await Task.CompletedTask;
        });
    }
}