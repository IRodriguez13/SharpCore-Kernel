using ShpCore.Kernel.Protocols;
using ShpCore.Iprotocol;
using MSharp.Launcher.Core.Protocols;

namespace ShpCore.Launcher.Core.Factory;

public static class ProtocolFactory
{
    public static IProtocol Get(string protocol)
    {
        return protocol.ToLowerInvariant() switch
        {
            "namedpipe" => new NamedPipeProtocol(),
            "remote-linux" => new RemoteLinuxProtocol(),
            "vm" => new SharpCoreVmProtocol(),
            // "grpc" => new GrpcProtocol(),
            // "unix" => new FileProtocol(),
            _ => throw new ArgumentException($"Unknown protocol: {protocol}")

        };
    }
}
