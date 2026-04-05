using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TTSAlbion.Albion;
using TTSAlbion.Converters;

namespace RequipAlbion.Network.Model;

public class EventPacket
{
    public EventPacket(short eventCode, Dictionary<byte, object> parameters)
    {
        EventCode = eventCode;
        Parameters = parameters;
    }

    public short EventCode { get; }
    public Dictionary<byte, object> Parameters { get; }
    
    // ================================
// EventPacket ToString
// ================================
    public override string ToString()
    {
        return $"[Albion] Event: {(EventCodes)EventCode} Params: {DebugFormatter.Format(Parameters)}";
    }
}