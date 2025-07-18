using ShpCore.Kernel.RemoteLinuxConnection;
using ShpCore.Logging;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Json;

namespace ShpCore.Kernel.RemoteHelper;

public static class BridgeSerializationHelper
{
    public static StringContent ToJsonContent<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    public static async Task<T?> FromJsonResponse<T>(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<T>();
    }

    public static T? FromJson<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json);
    }
}


