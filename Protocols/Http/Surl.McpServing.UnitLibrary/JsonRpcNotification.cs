using System.Text.Json;

namespace Surl.McpServing;

/// <summary>A JSON-RPC 2.0 notification: a method call with no id, which the server never answers.</summary>
public sealed class JsonRpcNotification : JsonRpcIncoming
{
    /// <summary>Initializes a new instance of the <see cref="JsonRpcNotification" /> class.</summary>
    /// <param name="method">The method the client calls.</param>
    /// <param name="parameters">The <c>params</c> object or array, or <see langword="null" /> when absent or JSON null.</param>
    public JsonRpcNotification(string method, JsonElement? parameters)
    {
        Method = method;
        Params = parameters;
    }

    /// <summary>Gets the method the client calls.</summary>
    public string Method { get; }

    /// <summary>Gets the <c>params</c> object or array, or <see langword="null" /> when absent or JSON null.</summary>
    public JsonElement? Params { get; }
}
