namespace Surl.Protocol.Abstractions;

/// <summary>
/// Where one exchange's events go (ADR-0004, section 5). The serving engine reports the bytes
/// in and out; the server adds notes for what the bytes do not say.
/// </summary>
/// <remarks>
/// A span passed to <see cref="BytesReceived"/> or <see cref="BytesSent"/> is valid only
/// during the call; an implementation that keeps bytes copies them.
/// </remarks>
public interface IExchangeLog
{
    /// <summary>
    /// Records bytes that arrived from the client.
    /// </summary>
    /// <param name="bytes">The bytes, valid only during the call.</param>
    void BytesReceived(ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Records bytes sent to the client.
    /// </summary>
    /// <param name="bytes">The bytes, valid only during the call.</param>
    void BytesSent(ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Records something the bytes do not say, such as a request refused and why.
    /// </summary>
    /// <param name="text">The note.</param>
    void Note(string text);
}
