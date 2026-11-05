namespace Surl.Tls13;

/// <summary>A Finished message (RFC 8446 section 4.4.4).</summary>
/// <param name="VerifyData">The HMAC that <see cref="TlsKeySchedule.ComputeFinishedVerifyData"/> computes, one hash long.</param>
public sealed record Finished(ReadOnlyMemory<byte> VerifyData)
{
    /// <summary>Writes the handshake message: type, length and body.</summary>
    /// <returns>The message bytes.</returns>
    public byte[] Write() =>
        TlsWriter.Handshake(TlsHandshakeType.Finished, writer => writer.WriteBytes(VerifyData.Span));
}
