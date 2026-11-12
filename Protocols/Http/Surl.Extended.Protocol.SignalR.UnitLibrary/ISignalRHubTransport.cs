namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// What carries one hub connection's bytes: WebSockets, Server-Sent Events with HTTP Post, or Long
/// Polling with HTTP Post (TransportProtocols.md; ADR-0119, decision 2 and its amendment 1). The
/// <see cref="SignalRHubSession"/> runs the hub over it and knows nothing of what is underneath.
/// </summary>
internal interface ISignalRHubTransport : IDisposable
{
    /// <summary>
    /// Held by the session around each write, so records are written one at a time; the transport takes it
    /// for any write of its own.
    /// </summary>
    SemaphoreSlim WriteGate { get; }

    /// <summary>
    /// Whether the transport carries the Binary transfer format, which the MessagePack hub protocol needs.
    /// </summary>
    bool CarriesBinary { get; }

    /// <summary>
    /// Waits for the client's next payload. Asking again tells a transport that acknowledges its payloads
    /// (HTTP Post) that the last one was processed.
    /// </summary>
    /// <param name="token">Stops the wait.</param>
    /// <returns>The payload, or how the connection ended.</returns>
    Task<SignalRTransportRead> ReadAsync(CancellationToken token);

    /// <summary>
    /// Sends one hub record, its <c>0x1E</c> included when the encoding has one.
    /// </summary>
    /// <param name="record">The record's bytes.</param>
    /// <param name="isBinary">Whether the connection speaks MessagePack, so the record is binary.</param>
    /// <param name="token">Cancels the write.</param>
    /// <returns>A task that completes once the record is handed to the transport.</returns>
    Task WriteRecordAsync(byte[] record, bool isBinary, CancellationToken token);

    /// <summary>
    /// Writes the connection's end and releases what the transport holds for it.
    /// </summary>
    /// <param name="end">How the connection ended.</param>
    /// <param name="hubClose">The hub Close record to send first, or <see langword="null"/>.</param>
    /// <param name="isBinary">Whether <paramref name="hubClose"/> is binary.</param>
    /// <param name="token">Cancels the write.</param>
    /// <returns>A task that completes when the end is written.</returns>
    Task WriteEndAsync(SignalRSessionEnd end, byte[]? hubClose, bool isBinary, CancellationToken token);
}
