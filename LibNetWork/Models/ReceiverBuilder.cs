using LibNetWork.Interfaces;

namespace LibNetWork.Models;

public sealed class ReceiverBuilder
{
    private readonly IPhotonParser _parser;

    public ReceiverBuilder(IPhotonParser parser)
    {
        _parser = parser;
    }

    public void AddHandler(IPacketHandler handler)
    {
        _parser.AddHandler(handler);
    }

    public IPhotonParser Build()
    {
        return _parser;
    }
}