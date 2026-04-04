using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RequipAlbion.Network.Model;

public class RequestPacket
{
    public RequestPacket(short operationCode, Dictionary<byte, object> parameters)
    {
        OperationCode = operationCode;
        Parameters = parameters;
    }

    public short OperationCode { get; }
    public Dictionary<byte, object> Parameters { get; }
}