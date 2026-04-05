using TTSAlbion.Albion.Models;
using TTSAlbion.Atributos;

namespace TTSAlbion.Albion.Handler.Event.Model;

public class MessageModel : ModelHandler
{
    public MessageModel(Dictionary<byte, object> parameters) : base(parameters)
    {
    }

    [Parse(0)]
    public int MessageType { get; set; }
    [Parse(1)]
    public string User { get; set; }
    [Parse(2)]
    public string Message { get; set; }
    [Parse(3)]
    public int Channel { get; set; }
    [Parse(252)]
    public int NoSaber  { get; set; }
}

public enum TypeMessage
{
    Global = 1,
    Espanol = 7,
    
}