namespace Surl.McpServing;

/// <summary>
/// A JSON-RPC 2.0 response or error the client sent: an object with <c>result</c> or <c>error</c> and no
/// <c>method</c>. The server makes no requests, so it ignores one (ADR-0103 decision 1).
/// </summary>
public sealed class JsonRpcClientResponse : JsonRpcIncoming
{
    private JsonRpcClientResponse()
    {
    }

    /// <summary>Gets the one instance; a client's response carries nothing the server reads.</summary>
    public static JsonRpcClientResponse Instance { get; } = new();
}
