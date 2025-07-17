using ShpCore.Kernel.Protocols;
using ShpCore.Iprotocol;
namespace ShpCore.Launcher.Core.Factory;

public static class ProtocolFactory
{
    public static IProtocol Get(string protocol)
    {
        return protocol.ToLowerInvariant() switch
        {
            "namedpipe" => new NamedPipeProtocol(),
            "remotelinux" => new RemoteLinuxProtocol(),
            // "grpc" => new GrpcProtocol(),
            // "unix" => new FileProtocol(),
            _ => throw new ArgumentException($"Unknown protocol: {protocol}")

        };
    }
}