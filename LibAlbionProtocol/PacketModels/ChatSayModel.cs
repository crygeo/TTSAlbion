using LibAlbionProtocol.Parsing;

namespace LibAlbionProtocol.PacketModels;

public class ChatSayModel : ModelHandler
{
    public ChatSayModel(Dictionary<byte, object> parameters) : base(parameters)
    {
    }

    [Parse(0)]
    public string User { get; set; }
    
     [Parse(1)]
    public string Message { get; set; }

    
}