namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// What the fault plan decided for one JSON-RPC request (<see cref="McpRequestFaults.ApplyAsync"/>):
/// answer it as usual, answer it with a chosen JSON-RPC error, or drop the exchange unanswered.
/// </summary>
/// <param name="ChosenError">The chosen JSON-RPC error response's bytes, or <see langword="null"/> for none.</param>
/// <param name="IsDropped">Whether a <c>drop</c> rule ended the exchange: nothing is written.</param>
internal readonly record struct McpFaultOutcome(byte[]? ChosenError, bool IsDropped)
{
    /// <summary>The request is answered as usual.</summary>
    public static McpFaultOutcome AnsweredAsUsual { get; } = new(null, false);

    /// <summary>The exchange is dropped.</summary>
    public static McpFaultOutcome Dropped { get; } = new(null, true);
}
