using ShpCore.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MSharp.Launcher.Core.Bridge;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using ShpCore.Kernel.RemoteHelper;

namespace ShpCore.Kernel.RemoteLinuxConnection;

public class RemoteLinuxBridgeConnection : IBridgeConnection
{
    private readonly HttpClient _client = new HttpClient();
    private readonly string _serviceUrl;

    public RemoteLinuxBridgeConnection(string url) => _serviceUrl = url ?? throw new ArgumentException("URL no puede ser nula");
    
    public void Start() => KernelLog.Info($"[RemoteBridge] Conectado a {_serviceUrl}");
    
    public void Send(string command)
    {
        try
        {

            var content = BridgeSerializationHelper.ToJsonContent(new { cmd = command });

            var response = _client.PostAsync($"{_serviceUrl}", content).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                var errorText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                KernelLog.Panic($"[RemoteBridge] Error remoto: {response.StatusCode} - {errorText}");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Remote Kernel Linux]: Error remoto: {errorText}");
                Console.ResetColor();
                return;
            }

            var result = response.Content.ReadFromJsonAsync<RemoteLinuxResult>().GetAwaiter().GetResult();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[Remote Kernel Linux]: {result?.Stdout?.Trim()}\n");
            Console.WriteLine($"[Remote Kernel Linux]: Exit Code: {result?.Exit_code}\n");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            KernelLog.Panic($"[RemoteBridge] Error ejecutando comando: {ex.Message}\n stacktrace: {ex.StackTrace}");
        }
    }

    public async Task SendAsync(string command)
    {
        try
        {
            var content = new StringContent(command ,Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(_serviceUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                KernelLog.Panic($"[RemoteBridge] Error remoto: {response.StatusCode} - {errorText}");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Remote Kernel Linux]: Error remoto: {errorText}");
                Console.ResetColor();
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<RemoteLinuxResult>();
            Console.WriteLine(result?.Stdout?.Trim()); // Salida directa y limpia
        }
        catch (Exception ex)
        {
            KernelLog.Panic($"[RemoteBridge] Error ejecutando comando: {ex.Message}");
        }
    }

    public void Dispose() => _client.Dispose();
}

// Modelo exacto para la respuesta del linux remoto
public class RemoteLinuxResult
{
    [JsonPropertyName("stdout")]
    public string Stdout { get; set; } = string.Empty;

    [JsonPropertyName("stderr")]
    public string Stderr { get; set; } = string.Empty;

    [JsonPropertyName("exit_code")]
    public int Exit_code { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; } = string.Empty;
}


