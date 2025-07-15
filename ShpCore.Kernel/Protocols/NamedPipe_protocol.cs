using ShpCore.Iprotocol;
using MSharp.Launcher.Core.Bridge;
using MSharp.Validation.Payloads;

namespace ShpCore.Kernel.Protocols;
public class NamedPipeProtocol : IProtocol
{
    public IBridgeConnection CreateBridge(string adapterPath)
    {
        return new NamedPipeBridgeConnection(pipeName: "namedpipe", adapter: new FinishAdapterLayer(adapterPath));
    }
}
