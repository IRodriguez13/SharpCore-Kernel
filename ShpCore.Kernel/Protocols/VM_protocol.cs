using ShpCore.Iprotocol;
using ShpCore.Kernel.Protocols;
using MSharp.Launcher.Core.Bridge;
using ShpCore.Kernel.VirtualMachineSubsystem;
using System.Text.Json;

namespace MSharp.Launcher.Core.Protocols;

public class SharpCoreVmProtocol : IProtocol
{
    public IBridgeConnection CreateBridge(string adapterPath)
    {
        var options = JsonSerializer.Deserialize<QemuOptions>(adapterPath);
        return new QemuBridgeConnection(options!);   
    }
}
