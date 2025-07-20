using MSharp.Launcher.Core.Bridge;
using System.Diagnostics;
using ShpCore.Kernel.RemoteLinuxConnection;
using ShpCore.Logging;
using System.Net.Sockets;
using System.Net;


namespace ShpCore.Kernel.VirtualMachineSubsystem;

public class QemuBridgeConnection : IBridgeConnection
{
    private readonly string _imagePath;
    private Process? _vmProcess;
    private int _forwardedPort = 5000;
    private readonly QemuOptions? _options;

    public QemuBridgeConnection(QemuOptions options)
    {
        _options = options;
        _imagePath = options.ImagePath ?? throw new ArgumentNullException(nameof(options.ImagePath));
        _forwardedPort = options.Port;
    }

    public Task SendAsync(string path) // No lo voy a usar por ahora.
    {
        return Task.Run(() =>
        {
            var remote = new RemoteLinuxBridgeConnection($"http://");
        }
        );
    }

    public void Start()
    {
        if (_options == null) throw new ArgumentNullException(nameof(_options));

        if (_options.Port == 0)
        {
            _forwardedPort = FindFreePort();
            KernelLog.Info($"[QEMU] Puerto libre asignado dinámicamente: {_forwardedPort}");
        }
        else
        {
            _forwardedPort = _options.Port;
        }

        KernelLog.Info($"[QEMU] Iniciando VM desde {_options.ImagePath}");

        string args = $"-hda {_options.ImagePath} -m {_options.MemoryMb} " +
                      $"-net nic -net user,hostfwd=tcp::{_forwardedPort}-:{_forwardedPort} " +
                      $"{(_options.UseSnapshot ? "-snapshot " : "")}" +
                      $"{(_options.UseNographic ? "-nographic" : "")} " +
                      $"{_options.ExtraArgs}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "qemu-system-x86_64",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _vmProcess = Process.Start(startInfo);

        KernelLog.Info($"[QEMU] Esperando a que el puerto {_forwardedPort} esté disponible...");
        WaitForPort("127.0.0.1", _forwardedPort);
    }

    public void Send(string command)
    {
        var remote = new RemoteLinuxBridgeConnection($"http://127.0.0.1:{_forwardedPort}/exec");
        remote.Send(command);
    }

    public void Dispose()
    {
        if (_vmProcess != null && !_vmProcess.HasExited)
        {
            KernelLog.Info("[QEMU] Apagando la VM");
            _vmProcess.Kill(true);
        }
    }

    private void WaitForPort(string host, int port, int timeoutSeconds = 15)
    {
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < timeoutSeconds)
        {
            try
            {
                using var client = new TcpClient();
                client.Connect(host, port);
                KernelLog.Info($"[QEMU] VM escuchando en http://127.0.0.1:{_forwardedPort}");
                return;
            }
            catch
            {
                Thread.Sleep(500);
            }
        }

        KernelLog.Panic("[QEMU] Timeout esperando a que la VM esté online");
        throw new Exception("QEMU VM did not start in time.");
    }

    private int FindFreePort(int startPort = 5000)
    {
        for (int port = startPort; port < startPort + 100; port++)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch { }
        }

        KernelLog.Panic("[QEMU] No se pudo encontrar un puerto libre para la VM");
        return -1;
    }
}


public class QemuOptions
{
    public string ImagePath { get; set; } = string.Empty;
    public int MemoryMb { get; set; } = 1024;
    public bool UseSnapshot { get; set; } = false;
    public bool UseNographic { get; set; } = true;
    public string ExtraArgs { get; set; } = string.Empty;
    public int Port { get; set; } = 0; // 0 = buscar uno libre

}
