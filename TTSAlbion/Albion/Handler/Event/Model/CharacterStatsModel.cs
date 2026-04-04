using TTSAlbion.Albion.Models;
using TTSAlbion.Atributos;

namespace TTSAlbion.Albion.Handler.Event.Model;

public class CharacterStatsModel : ModelHandler
{
    public CharacterStatsModel(Dictionary<byte, object> parameters) : base(parameters)
    {
    }

    [Parse(1)]
    public string NameUser { get; set; }
    
}