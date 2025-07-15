using MSharp.Launcher.Core.Bridge;

namespace ShpCore.Iprotocol;

public interface IProtocol
{
    IBridgeConnection CreateBridge(string adapter);
}
