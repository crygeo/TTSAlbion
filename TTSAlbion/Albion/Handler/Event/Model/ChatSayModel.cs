using TTSAlbion.Albion.Models;
using TTSAlbion.Atributos;

namespace TTSAlbion.Albion.Handler.Event.Model;

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