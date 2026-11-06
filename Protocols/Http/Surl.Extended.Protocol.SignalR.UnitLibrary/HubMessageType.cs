namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The hub-protocol message types the server reads and writes, numbered as the JSON encoding's
/// <c>type</c> member numbers them (HubProtocol.md "JSON Encoding"; ADR-0119, decision 3). Ack (8)
/// and Sequence (9) are BL-755's and read as protocol errors until then.
/// </summary>
public enum HubMessageType
{
    /// <summary>
    /// Invocation: calls a method, answered with a Completion unless it carries no <c>invocationId</c>.
    /// </summary>
    Invocation = 1,

    /// <summary>
    /// StreamItem: one item of a stream.
    /// </summary>
    StreamItem = 2,

    /// <summary>
    /// Completion: ends an invocation or a stream, with a result, an error or neither.
    /// </summary>
    Completion = 3,

    /// <summary>
    /// StreamInvocation: calls a streaming method, answered with StreamItems then a Completion.
    /// </summary>
    StreamInvocation = 4,

    /// <summary>
    /// CancelInvocation: stops a stream the caller started.
    /// </summary>
    CancelInvocation = 5,

    /// <summary>
    /// Ping: keeps the connection alive; needs no answer.
    /// </summary>
    Ping = 6,

    /// <summary>
    /// Close: ends the connection, with or without an error.
    /// </summary>
    Close = 7,
}
