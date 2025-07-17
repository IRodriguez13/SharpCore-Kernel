using ShpCore.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MSharp.Launcher.Core.Bridge;

namespace ShpCore.Kernel.RemoteLinuxConnection;

public class RemoteLinuxBridgeConnection : IBridgeConnection
{
    private readonly HttpClient _client;
    private const string BaseUrl = "http://localhost:5000"; // Cambialo si lo subís
    private string Urls
    {
        set
        {
            if(value.Length < 0)
            {
                KernelLog.Panic("[RemoteLinuxBridgeConnection] Dev, No se proporcionaron URLs para la conexión remota.");
                throw new ArgumentException();
            }
        }
    }

    public RemoteLinuxBridgeConnection(string Urls)
    {
        _client = new HttpClient();
    }

    public void Start()
    {
        KernelLog.Info("[RemoteBridge] Conexión lista al microservicio Linux remoto.");
    }

    public void Send(string payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { command = payload });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = _client.PostAsync($"{BaseUrl}/exec", content).Result;

            string responseBody = response.Content.ReadAsStringAsync().Result;

            KernelLog.Info($"[RemoteBridge] Respuesta del microservicio:\n{responseBody}");
        }
        catch (Exception ex)
        {
            KernelLog.Panic("[RemoteBridge] Error al enviar payload", ex);
        }
    }
}
