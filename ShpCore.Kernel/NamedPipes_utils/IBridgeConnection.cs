namespace MSharp.Launcher.Core.Bridge;

public interface IBridgeConnection // Contrato para la definición de conexiones entre C# y Java
{
    void Start();
    Task SendAsync(string command);
    void Send(string payload); // Versión síncrona
    // event Action<string> OnMessage;
}
