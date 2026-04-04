using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}