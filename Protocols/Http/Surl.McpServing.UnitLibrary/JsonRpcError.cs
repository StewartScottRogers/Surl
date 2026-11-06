namespace Surl.McpServing;

/// <summary>The <c>error</c> object of a JSON-RPC 2.0 error response, as ADR-0103 decision 1's table words it.</summary>
/// <param name="Code">The error code, one of <see cref="JsonRpcErrorCodes" />.</param>
/// <param name="Message">The short description of the error.</param>
/// <param name="ResourceUri">
/// The URI written as the error's <c>data.uri</c>, or <see langword="null" /> to write no <c>data</c>; only
/// <see cref="ResourceNotFound" /> sets it.
/// </param>
public sealed record JsonRpcError(int Code, string Message, string? ResourceUri = null)
{
    /// <summary>Gets the answer to a message that is not UTF-8, not JSON or nested too deep.</summary>
    public static JsonRpcError ParseError { get; } = new(JsonRpcErrorCodes.ParseError, "Parse error");

    /// <summary>Gets the answer to JSON that is not a JSON-RPC 2.0 request or notification.</summary>
    public static JsonRpcError InvalidRequest { get; } = new(JsonRpcErrorCodes.InvalidRequest, "Invalid Request");

    /// <summary>Gets the answer to a request for a method the server does not have.</summary>
    public static JsonRpcError MethodNotFound { get; } = new(JsonRpcErrorCodes.MethodNotFound, "Method not found");

    /// <summary>Gets the answer to a request other than <c>initialize</c> or <c>ping</c> before <c>initialize</c> was answered.</summary>
    public static JsonRpcError NotInitialized { get; } = new(JsonRpcErrorCodes.InvalidRequest, "Invalid Request: the session is not initialized");

    /// <summary>Gets the answer to a second <c>initialize</c>.</summary>
    public static JsonRpcError AlreadyInitialized { get; } = new(JsonRpcErrorCodes.InvalidRequest, "Invalid Request: the session is already initialized");

    /// <summary>
    /// Gets the answer to a tool handler that threw anything but <see cref="McpToolFailureException" /> or a
    /// cancellation; it carries no exception text, so no path or secret leaks.
    /// </summary>
    public static JsonRpcError InternalError { get; } = new(JsonRpcErrorCodes.InternalError, "Internal error");

    /// <summary>Creates the answer to a message longer than the session's limit.</summary>
    /// <param name="maximumMessageBytes">The limit, in bytes.</param>
    /// <returns>An <see cref="JsonRpcErrorCodes.InvalidRequest" /> error naming the limit.</returns>
    public static JsonRpcError MessageTooLong(int maximumMessageBytes) =>
        new(JsonRpcErrorCodes.InvalidRequest, $"Invalid Request: the message is longer than {maximumMessageBytes} bytes");

    /// <summary>Creates the answer to <c>resources/read</c> of a URI the server does not have.</summary>
    /// <param name="uri">The URI asked for, written as <c>data.uri</c>.</param>
    /// <returns>A <see cref="JsonRpcErrorCodes.ResourceNotFound" /> error naming the URI.</returns>
    public static JsonRpcError ResourceNotFound(string uri) =>
        new(JsonRpcErrorCodes.ResourceNotFound, "Resource not found", uri);

    /// <summary>Creates the answer to parameters that are missing or of the wrong kind.</summary>
    /// <param name="what">What is missing or wrong, e.g. <c>protocolVersion</c>.</param>
    /// <returns>An <see cref="JsonRpcErrorCodes.InvalidParams" /> error naming it.</returns>
    public static JsonRpcError InvalidParams(string what) =>
        new(JsonRpcErrorCodes.InvalidParams, $"Invalid params: {what}");
}
