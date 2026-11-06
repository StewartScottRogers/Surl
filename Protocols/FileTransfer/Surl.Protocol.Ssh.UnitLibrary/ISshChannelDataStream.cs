namespace Surl.Protocol.Ssh;

/// <summary>
/// The data of one session channel as its handler sees it: what the client sends, and what
/// the handler sends back, each within the window its reader granted (RFC 4254, section 5.2).
/// </summary>
internal interface ISshChannelDataStream
{
    /// <summary>
    /// Reads the client's next channel data into <paramref name="buffer"/>, waiting for some if
    /// none is buffered; the bytes read are granted back to the client once half the window is used.
    /// </summary>
    /// <param name="buffer">Where the data goes.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>How many bytes were read; 0 once the client sent <c>EOF</c> or closed the channel.</returns>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>
    /// Sends <paramref name="data"/> to the client as channel data, in packets no larger than the
    /// client's maximum packet, waiting for the client to widen its window when it is used up.
    /// </summary>
    /// <param name="data">The bytes to send.</param>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>A task that completes when every byte is written.</returns>
    /// <exception cref="IOException">
    /// The channel is closed - the client closed it or the connection ended - while bytes remain
    /// to be sent. A packet already on its way when the channel closes is dropped.
    /// </exception>
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
}
