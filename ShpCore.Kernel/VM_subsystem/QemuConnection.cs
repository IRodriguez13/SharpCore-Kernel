using MSharp.Launcher.Core.Bridge;
using System.Diagnostics;
using ShpCore.Kernel.RemoteLinuxConnection;
using ShpCore.Logging;
using System.Net.Sockets;

namespace ShpCore.Kernel.VirtualMachineSubsystem;

public class QemuBridgeConnection : IBridgeConnection
{
    private readonly string _imagePath;
    private Process? _vmProcess;
    private readonly int _forwardedPort = 5000;
    private readonly QemuOptions? _options;

    public QemuBridgeConnection(QemuOptions options)
    {
        _options = options; ;
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
        if (_options == null) throw new ArgumentNullException(nameof(_options), "QemuOptions cannot be null");

        KernelLog.Info($"[QEMU] Iniciando VM desde {_options.ImagePath}");

        string args = $"-hda {_options.ImagePath} -m {_options.MemoryMb} " +
                      $"-net nic -net user,hostfwd=tcp::{_options.Port}-:{_options.Port} " +
                      $"{(_options.UseSnapshot ? "-snapshot " : "")}" +
                      $"{(_options.UseNographic ? "-nographic" : "")}";

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

        KernelLog.Info("[QEMU] Esperando a que el puerto 5000 esté disponible...");
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

    private void WaitForPort(string host, int port, int timeoutSeconds = 10)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < timeoutSeconds)
        {
            try
            {
                using var client = new TcpClient();
                client.Connect(host, port);
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
}


public class QemuOptions
{
    public string ImagePath { get; set; } = string.Empty;
    public int MemoryMb { get; set; } = 1024;
    public int Port { get; set; } = 5000;
    public bool UseSnapshot { get; set; } = false;
    public bool UseNographic { get; set; } = true;
}
