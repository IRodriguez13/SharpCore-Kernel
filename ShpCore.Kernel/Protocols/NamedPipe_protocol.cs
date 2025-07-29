using ShpCore.Iprotocol;
using MSharp.Launcher.Core.Bridge;
using MSharp.Validation.Payloads;

namespace ShpCore.Kernel.Protocols;
public class NamedPipeProtocol : IProtocol //No se si es buena idea tener una clase por cada implementacion de IProtocol
{
    public IBridgeConnection CreateBridge(string adapterPath)
    {
        return new NamedPipeBridgeConnection(pipeName: "namedpipe", adapter: new FinishAdapterLayer(adapterPath));
    }
}
