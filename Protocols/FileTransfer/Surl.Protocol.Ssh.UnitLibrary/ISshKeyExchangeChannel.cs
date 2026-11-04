namespace Surl.Protocol.Ssh;

/// <summary>
/// The messages a key exchange method reads and writes, between the two <c>KEXINIT</c>
/// messages and <c>NEWKEYS</c>.
/// </summary>
internal interface ISshKeyExchangeChannel
{
    /// <summary>
    /// Reads the client's next key exchange message, which must be
    /// <paramref name="messageNumber"/>; generic transport messages in between are handled as
    /// <see cref="SshTransportHandshake"/> says.
    /// </summary>
    /// <param name="messageNumber">The message the method expects.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>A reader over the message, past its message number.</returns>
    /// <exception cref="SshDisconnectRequiredException">Another message came: <c>DISCONNECT</c> 2.</exception>
    ValueTask<SshWireReader> ReadAsync(byte messageNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Writes one message to the client.
    /// </summary>
    /// <param name="payload">The message, message number first.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>A task that completes once the message is handed to the transport.</returns>
    ValueTask WriteAsync(byte[] payload, CancellationToken cancellationToken);
}
