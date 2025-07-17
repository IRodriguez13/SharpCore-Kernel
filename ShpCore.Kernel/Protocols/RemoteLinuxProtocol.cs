using ShpCore.Iprotocol;
using MSharp.Launcher.Core.Bridge;
using MSharp.Validation.Payloads;
using ShpCore.Kernel.RemoteLinuxConnection;
using ShpCore.Logging;

namespace ShpCore.Kernel.Protocols;

public class RemoteLinuxProtocol : IProtocol
{
    public IBridgeConnection CreateBridge(string adapterPath)
    {

        var urls = adapterPath.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (urls.Length == 0)
        {
            KernelLog.Panic("[RemoteLinuxProtocol] Adapterpath vacío.");
            throw new ArgumentException(nameof(adapterPath));
        }

        if (urls.Length == 0)
        {
            KernelLog.Panic("[RemoteLinuxProtocol] Dev, No se proporcionaron URLs para la conexión remota.");
            throw new ArgumentException();;
        }


        // Caso de un solo endpoint
        if (urls.Length == 1)

            return new RemoteLinuxBridgeConnection(urls[0]);


        // Caso múltiples endpoints (requiere implementar CompositeBridge)

        var connections = urls.Select(url => new RemoteLinuxBridgeConnection(url)).ToList();

        return new CompositeLinuxBridgeConnection(connections);
    }
}



