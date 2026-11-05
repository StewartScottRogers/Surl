namespace Surl.McpServing;

/// <summary>
/// The JSON-RPC 2.0 error codes an MCP server answers with: the five the JSON-RPC 2.0 specification
/// defines and MCP's resource-not-found code (ADR-0103 decision 1).
/// </summary>
public static class JsonRpcErrorCodes
{
    /// <summary>The message is not UTF-8, not JSON, or nests deeper than 64 levels.</summary>
    public const int ParseError = -32700;

    /// <summary>The JSON is not a JSON-RPC 2.0 request or notification the session accepts now.</summary>
    public const int InvalidRequest = -32600;

    /// <summary>The method does not exist.</summary>
    public const int MethodNotFound = -32601;

    /// <summary>The method's parameters are invalid.</summary>
    public const int InvalidParams = -32602;

    /// <summary>The server failed while answering the request.</summary>
    public const int InternalError = -32603;

    /// <summary>MCP's code for a resource URI the server does not have.</summary>
    public const int ResourceNotFound = -32002;
}
