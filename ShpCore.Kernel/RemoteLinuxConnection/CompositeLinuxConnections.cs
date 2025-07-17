using ShpCore.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MSharp.Launcher.Core.Bridge;

public class CompositeLinuxBridgeConnection : IBridgeConnection
{
    private readonly IReadOnlyList<IBridgeConnection> _connections;
    private readonly HttpClient _client;
    private const string BaseUrl = "http://localhost:5000";
    public CompositeLinuxBridgeConnection(IEnumerable<IBridgeConnection> connections)
    {

        _connections = connections.ToList();
        _client = new HttpClient();
    }
    
    public void Start() => KernelLog.Info("[CompositeBridge] Iniciando conexiones a microservicios remotos.");
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