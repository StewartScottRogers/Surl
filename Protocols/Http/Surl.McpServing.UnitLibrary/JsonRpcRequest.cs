using System.Text.Json;

namespace Surl.McpServing;

/// <summary>A JSON-RPC 2.0 request: a method call with an id the server answers.</summary>
public sealed class JsonRpcRequest : JsonRpcIncoming
{
    /// <summary>Initializes a new instance of the <see cref="JsonRpcRequest" /> class.</summary>
    /// <param name="id">The id the response echoes.</param>
    /// <param name="method">The method the client calls.</param>
    /// <param name="parameters">The <c>params</c> object or array, or <see langword="null" /> when absent or JSON null.</param>
    public JsonRpcRequest(JsonRpcRequestId id, string method, JsonElement? parameters)
    {
        Id = id;
        Method = method;
        Params = parameters;
    }

    /// <summary>Gets the id the response echoes.</summary>
    public JsonRpcRequestId Id { get; }

    /// <summary>Gets the method the client calls.</summary>
    public string Method { get; }

    /// <summary>Gets the <c>params</c> object or array, or <see langword="null" /> when absent or JSON null.</summary>
    public JsonElement? Params { get; }
}
