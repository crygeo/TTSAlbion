using LibAlbionProtocol.Parsing;

namespace LibAlbionProtocol.PacketModels;

public class CharacterStatsModel : ModelHandler
{
    public CharacterStatsModel(Dictionary<byte, object> parameters) : base(parameters)
    {
    }

    [Parse(1)]
    public string NameUser { get; set; }
    
}