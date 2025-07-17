using ShpCore.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MSharp.Launcher.Core.Bridge;
using System.Text.Json.Serialization;
using System.Net.Http.Json;

namespace ShpCore.Kernel.RemoteLinuxConnection;

public class RemoteLinuxBridgeConnection : IBridgeConnection
{
    private readonly HttpClient _client = new HttpClient();
    private readonly string _serviceUrl;

    public RemoteLinuxBridgeConnection(string url)
    {
        _serviceUrl = url ?? throw new ArgumentException("URL no puede ser nula");
    }

    public void Start()
    {
        KernelLog.Info($"[RemoteBridge] Conectado a {_serviceUrl}");
    }

    public void Send(string command)
    {
        try
        {

            var content = new StringContent(command, Encoding.UTF8, "application/json");

            // 2. Enviar la solicitud (versión síncrona)
            var response = _client.PostAsync($"{_serviceUrl}", content).GetAwaiter().GetResult();

            // 3. Leer la respuesta (versión síncrona)
            var result = response.Content.ReadFromJsonAsync<RemoteLinuxResult>().GetAwaiter().GetResult();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[Kernel Linux]: {result?.Stdout?.Trim()}\n[Kernel Linux] Exit Code: {result?.Exit_code}\n");
            Console.ResetColor();
        }
        catch (HttpRequestException httpEx)
        {
            KernelLog.Panic($"Fallo HTTP: {httpEx.Message}");
        }
        catch (Exception ex)
        {
            var errorContent = _client.GetStringAsync(_serviceUrl).Result;
            KernelLog.Panic($"[RemoteBridge] Error ejecutando comando: {ex.Message}\nContenido del error: {errorContent}");
        }
        finally
        {
            _client.Dispose();
        }
    }

    public async Task SendAsync(string command)
    {
        try
        {
            // 1. Construir el payload EXACTAMENTE como en curl
            var payload = new { command };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 2. Enviar a la URL exacta (sin agregar /exec)
            var response = await _client.PostAsync(_serviceUrl, content);

            // 3. Leer y mostrar la respuesta directa (sin decoración)
            var result = await response.Content.ReadFromJsonAsync<RemoteLinuxResult>();
            Console.WriteLine(result?.Stdout?.Trim()); // Salida directa y limpia
        }
        catch (Exception ex)
        {
            KernelLog.Panic($"Error ejecutando comando: {ex.Message}");
        }
    }

    public void Dispose() => _client.Dispose();
}

// Modelo exacto para la respuesta
public class RemoteLinuxResult
{
    [JsonPropertyName("stdout")]
    public string Stdout { get; set; } = string.Empty;

    [JsonPropertyName("stderr")]
    public string Stderr { get; set; } = string.Empty;

    [JsonPropertyName("exit_code")]
    public int Exit_code { get; set; }
}
