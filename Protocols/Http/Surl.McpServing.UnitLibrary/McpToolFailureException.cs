namespace Surl.McpServing;

/// <summary>
/// Thrown by a tool's handler to report a failure inside the tool: the core answers it as a <c>tools/call</c>
/// result with <c>isError</c> true and the message as its one text block, never as a JSON-RPC error
/// (ADR-0103 decision 1). Any other exception is answered <c>-32603 Internal error</c> without its text.
/// </summary>
public sealed class McpToolFailureException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="McpToolFailureException" /> class.</summary>
    public McpToolFailureException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="McpToolFailureException" /> class.</summary>
    /// <param name="message">What went wrong, shown to the client.</param>
    public McpToolFailureException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="McpToolFailureException" /> class.</summary>
    /// <param name="message">What went wrong, shown to the client.</param>
    /// <param name="innerException">The exception that caused it.</param>
    public McpToolFailureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
