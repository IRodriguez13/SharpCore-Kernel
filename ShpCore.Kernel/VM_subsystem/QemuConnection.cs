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

    public Task SendAsync(string path) // I wont use it for now. Just for the interface errs
    {
        return Task.Run(() =>
        {
            var remote = new RemoteLinuxBridgeConnection($"http://");
        });
    }

    private void PreflightCheck() // Checkeo previo a la ejecución de la VM
    {
        KernelLog.Warn("[Preflight] Running QEMU preflight Healthcheck...");

        if (_options == null)
        {
            KernelLog.Panic("[Preflight QemuConnection.cs line:44] QEMU options undefined .");
            throw new ArgumentNullException(nameof(_options));
        }

        // 1. Validar imagen
        if (string.IsNullOrWhiteSpace(_options.ImagePath) || !File.Exists(_options.ImagePath))
        {
            KernelLog.Panic($"[Preflight] Linux Image not found: {_options.ImagePath}");
            throw new FileNotFoundException("La imagen no existe.", _options.ImagePath);
        }

        // 2. Validar SharedFolder o asignar por defecto según OS
        if (string.IsNullOrWhiteSpace(_options.SharedFolder))
        {
            var userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _options.SharedFolder = _options.SharedFolder.Replace("\\", "/");
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

            KernelLog.Warn($"[Preflight for Mounting System] Shared folder not found. Using fallback: {_options.SharedFolder}");
        }

        // 4 - Create folder if not exists
        try
        {
            if (!Directory.Exists(_options.SharedFolder))
            {
                Directory.CreateDirectory(_options.SharedFolder);
                KernelLog.Info($"[Preflight] Shared Folder created : {_options.SharedFolder}");
            }
        }
        catch (Exception ex)
        {
            KernelLog.Panic($"[Preflight QemuConnection line:87] Failure creating the Shared folder for Mount: {ex.Message}");
            throw;
        }


        //  Asignación dinámica de puerto si es necesario
        if (_options.Port == 0)
        {
            _options.Port = FindFreePort();
            KernelLog.Info($"[Preflight] Free port dynamically assigned: {_options.Port}");
        }
        else
        {
            KernelLog.Info($"[Preflight] Using port: {_options.Port}");
        }

        // Limpieza de QEMU si está usando la imagen específica
        var qemuProcs = Process.GetProcessesByName("qemu-system-x86_64");
        foreach (var proc in qemuProcs)
        {
            try
            {
                if (proc.StartInfo.Arguments.Contains(_options.ImagePath))

                    proc.Kill(true);
                proc.WaitForExit(1500);
                KernelLog.Info($"[Preflight] Proceso QEMU colgado eliminado: PID {proc.Id}");

            }
            catch
            {
                KernelLog.Warn($"[Preflight] Couldnt kill QEMU process with id: {proc.Id}. Maybe it already finished.");
            }
        }

        KernelLog.Info("[Preflight] All checks passed. Ready to boot");
    }


    public void Start()
    {
        if (_options == null) throw new ArgumentNullException(nameof(_options));


        if (_options.Port == 0)

            _options.Port = FindFreePort();
        KernelLog.Info($"[QEMU] Puerto libre asignado dinámicamente: {_options.Port}");



        KernelLog.Info($"[QEMU] Puerto libre asignado dinámicamente: {_options.Port}");

        PreflightCheck();

        KernelLog.Info($"[QEMU] Starting VM from: {_options.ImagePath}");


        string virtfsArg = string.Empty;
        if (!string.IsNullOrEmpty(_options.SharedFolder))
        {
            virtfsArg = $"-virtfs local,path={_options.SharedFolder},mount_tag=hostshare,security_model=passthrough,id=hostshare ";
        }

        string args = string.Join(" ",
       $"-hda {_options.ImagePath}",
       $"-m {_options.MemoryMb}",
       "-net nic",
       $"-net user,hostfwd=tcp::{_options.Port}-:{_options.Port}",
       virtfsArg,
       _options.UseSnapshot ? "-snapshot" : "",
       _options.UseNographic ? "-nographic" : "",
       _options.ExtraArgs
   );


        bool redirectOutput = !_options.UseNographic;

        var startInfo = new ProcessStartInfo
        {
            FileName = "qemu-system-x86_64",
            Arguments = args,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput,
            UseShellExecute = !redirectOutput,
            CreateNoWindow = redirectOutput
        };

        _vmProcess = new Process { StartInfo = startInfo };

        if (redirectOutput)
        {
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
        }

        bool process = _vmProcess.Start();

        if (redirectOutput)
        {
            _vmProcess.BeginOutputReadLine();
            _vmProcess.BeginErrorReadLine();
        }

        KernelLog.Info($"[QEMU] Waiting for port: {_options.Port}");
        WaitForPort("127.0.0.1", _options.Port);

        if (_options.UseSnapshot)
        {
            _vmProcess.WaitForExit();
            KernelLog.Info("[QEMU] VM finished succesfully.");
        }
    }

    public void Send(string command)
    {
        if (_options == null)
        {
            KernelLog.Panic("[Preflight] QEMU options undefined.");
            throw new ArgumentNullException(nameof(_options));
        }

        var remote = new RemoteLinuxBridgeConnection($"http://127.0.0.1:{_options.Port}/exec");
        remote.Send(command);
    }

    public void Dispose()
    {
        if (_vmProcess is null)
        {
            KernelLog.Panic("[QEMU QemuConnection line:226] The process is null");
        }
        if (!_vmProcess.HasExited)
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
                    KernelLog.Panic("[Preflight] QEMU otions undefined.");
                    throw new ArgumentNullException(nameof(_options));
                }

                KernelLog.Info($"[QEMU] VM listening on: http://127.0.0.1:{_options.Port}");
                return;
            }
            catch
            {
                Thread.Sleep(500);
            }
        }

        KernelLog.Panic("[QEMU] Timeout reached. VM didnt start on time");
        throw new Exception("QEMU VM did not start in time.");
    }

    private int FindFreePort(int startPort = 5000) // Busca un puerto libre a partir del puerto especificado
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        if (port == 0)
        {
            KernelLog.Panic("[QEMU] Couldnt find free port.");
            throw new Exception("No free port found for QEMU.");
        }

        return port;
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
        		Console.ForegroundColor = ConsoleColor.Green;
        		Console.WriteLine("OK");
        		Console.ResetColor();
            KernelLog.Info("[MountCheck]  Shared folder succesfully mounted.");
            return true;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAIL");
            Console.ResetColor(); 
            KernelLog.Warn("[MountCheck] Montaje no detectado en /mnt/hostshare.");
            return false;
        }
    }
}


public class QemuOptions
{
    public string? ImagePath { get; set; }
    public int MemoryMb { get; set; }
    public bool UseSnapshot { get; set; }
    public bool UseNographic { get; set; }
    public string? ExtraArgs { get; set; }
    public int Port { get; set; }
    public int StartPort { get; set; }
    public string? SharedFolder { get; set; }
}


