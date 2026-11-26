using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// An <see cref="IReceivedBytesRedactor"/> that withholds every byte it is given: it writes
/// <see cref="LineBytesRedactor.SecretMask"/> once, at the first byte, and nothing after it. It
/// serves the rest of a SOCKS tunnel whose bytes it cannot tell secrets apart in (ADR-0182,
/// amendment 7).
/// </summary>
internal sealed class SocksWithheldBytesRedactor : IReceivedBytesRedactor
{
    private bool isMarked;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        if (!isMarked && received.Length > 0)
        {
            isMarked = true;
            output.Write(LineBytesRedactor.SecretMask);
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // Nothing the origin sends makes a withheld byte safe to log.
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        // Nothing is held back.
    }
}
