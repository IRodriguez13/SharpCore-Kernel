using MSharp.Launcher.Core.Bridge;
using System.Diagnostics;
using ShpCore.Kernel.RemoteLinuxConnection;
using ShpCore.Logging;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;


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

    private void PreflightCheck()
    {
        KernelLog.Warn("[Preflight] Iniciando healthcheck previos para QEMU");

        if (_options == null)
        {
            KernelLog.Panic("[Preflight] Opciones de QEMU no definidas.");
            throw new ArgumentNullException(nameof(_options));
        }

        // 1. Validar imagen
        if (string.IsNullOrWhiteSpace(_options.ImagePath) || !File.Exists(_options.ImagePath))
        {
            KernelLog.Panic($"[Preflight] Imagen no encontrada: {_options.ImagePath}");
            throw new FileNotFoundException("La imagen de disco no existe.", _options.ImagePath);
        }

        // 2. Validar SharedFolder o asignar por defecto según OS
        if (string.IsNullOrWhiteSpace(_options.SharedFolder))
        {
            var userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _options.SharedFolder = Path.Combine(userDir, "SharpCoreShare");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _options.SharedFolder = Path.Combine(userDir, "sharpcore-share-mac");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _options.SharedFolder = Path.Combine(userDir, "sharpcore-share");
            }

            KernelLog.Warn($"[Preflight] Carpeta compartida no definida. Usando fallback: {_options.SharedFolder}");
        }

        // 3. Sanitizar path para Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _options.SharedFolder = _options.SharedFolder.Replace("\\", "/");
        }

        // 4. Crear carpeta si no existe
        try
        {
            if (!Directory.Exists(_options.SharedFolder))
            {
                Directory.CreateDirectory(_options.SharedFolder);
                KernelLog.Info($"[Preflight] Carpeta compartida creada: {_options.SharedFolder}");
            }
        }
        catch (Exception ex)
        {
            KernelLog.Panic($"[Preflight] Error al crear carpeta compartida: {ex.Message}");
            throw;
        }


        //  Asignación dinámica de puerto si es necesario
        if (_options.Port == 0)
        {
            _options.Port = FindFreePort();
            KernelLog.Info($"[Preflight] Puerto libre asignado dinámicamente: {_options.Port}");
        }
        else
        {
            KernelLog.Info($"[Preflight] Usando puerto especificado: {_options.Port}");
        }

        // Limpieza de QEMU si está usando la imagen específica
        var qemuProcs = Process.GetProcessesByName("qemu-system-x86_64");
        foreach (var proc in qemuProcs)
        {
            try
            {
                if (proc.StartInfo.Arguments.Contains(_options.ImagePath))
                {
                    proc.Kill(true);
                    proc.WaitForExit(1500);
                    KernelLog.Info($"[Preflight] Proceso QEMU colgado eliminado: PID {proc.Id}");
                }
            }
            catch
            {
                KernelLog.Warn($"[Preflight] No se pudo eliminar el proceso QEMU con PID {proc.Id}. Puede que ya haya finalizado.");
            }
        }

        KernelLog.Info("[Preflight] Todos los chequeos pasaron correctamente. Ready to boot");
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

    public bool IsHostshareMounted()
    {
        if (_options == null)
        {
            KernelLog.Panic("[MountCheck] Opciones de QEMU no definidas.");
            throw new ArgumentNullException(nameof(_options));
        }
        
        var remote = new RemoteLinuxBridgeConnection($"http://127.0.0.1:{_options.Port}/exec");
        var result = remote.SendAndReceive("mount | grep /mnt/hostshare");

        if (!string.IsNullOrWhiteSpace(result) && result.Contains("9p"))
        {
            KernelLog.Info("[MountCheck] Montaje de carpeta hostshare OK.");
            return true;
        }
        else
        {
            KernelLog.Warn("[MountCheck] Montaje no detectado en /mnt/hostshare.");
            return false;
        }
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
    public string SharedFolder { get; set; } = string.Empty; // ¡Personalizable!
}


