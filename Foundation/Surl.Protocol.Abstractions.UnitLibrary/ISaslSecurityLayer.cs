namespace Surl.Protocol.Abstractions;

/// <summary>
/// The SASL security layer an accepted login negotiated (RFC 4422 section 3.7; ADR-0072,
/// decision 4): it protects every message after the successful login's answer, in both
/// directions. The server owns the framing - a 4-byte big-endian length then the protected
/// bytes - and hands this layer only what lies inside it. One layer serves one connection and
/// keeps its sequence numbers and cipher state from one message to the next.
/// </summary>
public interface ISaslSecurityLayer
{
    /// <summary>
    /// The most protected bytes one buffer from the peer may hold; a longer one ends the connection.
    /// </summary>
    int MaximumProtectedBytes { get; }

    /// <summary>
    /// Protects one message the server sends.
    /// </summary>
    /// <param name="message">The message in clear.</param>
    /// <returns>The protected bytes, without the length the server puts before them.</returns>
    byte[] Protect(ReadOnlySpan<byte> message);

    /// <summary>
    /// Checks and unprotects one buffer the peer sent.
    /// </summary>
    /// <param name="buffer">The protected bytes, without their length.</param>
    /// <param name="message">The message in clear when the buffer passes; empty otherwise.</param>
    /// <returns>
    /// <see langword="false"/> when the buffer fails its signature or MAC or is out of sequence,
    /// which ends the connection with no answer.
    /// </returns>
    bool TryUnprotect(ReadOnlySpan<byte> buffer, out byte[] message);
}
