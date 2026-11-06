namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// Writes the <c>text/event-stream</c> body a <c>tools/call</c> is answered with (ADR-0111 decisions 4.3 and
/// 4.8), by the WHATWG HTML "Server-sent events" section: one event of the default type, its one <c>data:</c>
/// line holding the JSON-RPC response, then the blank line that dispatches it. No <c>event:</c> and no
/// <c>id:</c> field is written. A cancelled call ends the stream with no event.
/// </summary>
internal static class McpEventStream
{
    /// <summary>The media type of the stream.</summary>
    public const string MediaType = "text/event-stream";

    private static readonly byte[] DataFieldPrefix = "data: "u8.ToArray();

    private static readonly byte[] EventEnd = "\n\n"u8.ToArray();

    /// <summary>Writes the stream's body.</summary>
    /// <param name="response">The JSON-RPC response, on one line, or <see langword="null" /> when the call was cancelled.</param>
    /// <returns>The body: one event, or nothing.</returns>
    public static byte[] Write(byte[]? response) =>
        response is null ? [] : [.. DataFieldPrefix, .. response, .. EventEnd];
}
