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
    private readonly QemuOptions? _options;

    public QemuBridgeConnection(QemuOptions options)
    {
        _options = options;
        _imagePath = options.ImagePath ?? throw new ArgumentNullException(nameof(options.ImagePath));
        if (_options.Port == 0)
        {
            _options.Port = FindFreePort(_options.StartPort);
            KernelLog.Info($"[QEMU] Puerto libre asignado dinámicamente: {_options.Port}");
        }

    }

    public Task SendAsync(string path) // No lo voy a usar por ahora.
    {
        return Task.Run(() =>
        {
            var remote = new RemoteLinuxBridgeConnection($"http://");
        }
        );
    }

    private void PreflightCheck() // Realiza chequeos previos antes de iniciar la VM
    {
        KernelLog.Warn("[Preflight] Iniciando healthcheck previos para QEMU");

        if (_options == null)
        {
            KernelLog.Panic("[Preflight] Opciones de QEMU no definidas.");
            throw new ArgumentNullException(nameof(_options));
        }

        // 1. Validación de imagen
        if (!File.Exists(_options.ImagePath) || _options.ImagePath.Length == 0)
        {
            KernelLog.Panic($"[Preflight] Imagen no encontrada: {_options.ImagePath}");
            throw new FileNotFoundException("La imagen de disco no existe.", _options.ImagePath);
        }

        // 2. Validación y creación del folder compartido
        if (!string.IsNullOrEmpty(_options.SharedFolder))
        {
            if (!Directory.Exists(_options.SharedFolder))
            {
                Directory.CreateDirectory(_options.SharedFolder);
                KernelLog.Info($"[Preflight] Carpeta compartida creada: {_options.SharedFolder}");
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            opts.SharedFolder = "C:\\Users\\Public\\SharpCoreShare";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            opts.SharedFolder = "/home/tuusuario/sharpcore-share";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            opts.SharedFolder = "/Users/tuusuario/sharpcore-share";
        }


        // 3. Validación de puerto libre
        if (_options.Port == 0)
        {
            _options.Port = FindFreePort();
            KernelLog.Info($"[Preflight] Puerto libre asignado dinámicamente: {_options.Port}");
        }

        // 4. Limpieza de procesos zombie de QEMU
        var qemuProcs = Process.GetProcessesByName("qemu-system-x86_64");
        foreach (var proc in qemuProcs)
        {
            try
            {
                proc.Kill(true);
                proc.WaitForExit(1500); // Espera hasta 1.5 segundos para que el proceso termine
                KernelLog.Info($"[Preflight] Proceso QEMU colgado eliminado: PID {proc.Id}");
            }
            catch
            {
                KernelLog.Info($"[Preflight] No hay procesos QEMU en paralelo, Preflight OK.");
            }
        }
    }


    public void Start()
    {
        if (_options == null) throw new ArgumentNullException(nameof(_options));

        if (_options.Port == 0)
        {
            _options.Port = FindFreePort();
            KernelLog.Info($"[QEMU] Puerto libre asignado dinámicamente: {_options.Port}");
        }

        PreflightCheck();

        KernelLog.Info($"[QEMU] Iniciando VM desde {_options.ImagePath}");


        string virtfsArg = string.Empty;
        if (!string.IsNullOrEmpty(_options.SharedFolder))
        {
            virtfsArg = $"-virtfs local,path={_options.SharedFolder},mount_tag=hostshare,security_model=passthrough,id=hostshare ";
        }

        string args =
            $"-hda {_options.ImagePath} -m {_options.MemoryMb} " +
            $"-net nic -net user,hostfwd=tcp::{_options.Port}-:{_options.Port} " +
            virtfsArg +
            $"{(_options.UseSnapshot ? "-snapshot " : "")}" +
            $"{(_options.UseNographic ? "-nographic " : "")}" +
            $"{_options.ExtraArgs}";


        var startInfo = new ProcessStartInfo
        {
            FileName = "qemu-system-x86_64",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = false // true si querés ocultar ventana, false para debug
        };

        _vmProcess = new Process { StartInfo = startInfo };

        _vmProcess.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                KernelLog.Info($"[QEMU stdout] {e.Data}");
        };
        _vmProcess.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                KernelLog.Panic($"[QEMU stderr] {e.Data}");
        };

        _vmProcess.Start();
        _vmProcess.BeginOutputReadLine();
        _vmProcess.BeginErrorReadLine();

        KernelLog.Info($"[QEMU] Esperando a que el puerto {_options.Port} esté disponible...");
        WaitForPort("127.0.0.1", _options.Port);
        _vmProcess.WaitForExit();
        KernelLog.Info("[QEMU] VM finalizó correctamente.");

    }

    public void Send(string command)
    {
        if (_options == null)
        {
            KernelLog.Panic("[Preflight] Opciones de QEMU no definidas.");
            throw new ArgumentNullException(nameof(_options));
        }

        var remote = new RemoteLinuxBridgeConnection($"http://127.0.0.1:{_options.Port}/exec");
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

                if (_options == null)
                {
                    KernelLog.Panic("[Preflight] Opciones de QEMU no definidas.");
                    throw new ArgumentNullException(nameof(_options));
                }

                KernelLog.Info($"[QEMU] VM escuchando en http://127.0.0.1:{_options.Port}");
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

    private int FindFreePort(int startPort = 5000) // Busca un puerto libre a partir del puerto especificado
    {
        for (int port = 0; port < startPort + 100; port++)
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
    public int StartPort { get; set; } = 5000;
    public string SharedFolder { get; set; } = "/path/to/share"; // ¡Personalizable!
}
