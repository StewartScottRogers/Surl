using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// The <see cref="IReceivedBytesRedactor"/> for the bytes after an HTTP/2 connection preface that
/// did not start where a request may start (ADR-0182): the frames cannot be placed for certain, so
/// <see cref="LineBytesRedactor.SecretMask"/> is written once, at the first byte, and nothing else
/// the client sends is logged.
/// </summary>
internal sealed class HttpProxyWithheldBytesRedactor : IReceivedBytesRedactor
{
    private bool maskWritten;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (maskWritten || received.IsEmpty)
        {
            return;
        }

        output.Write(LineBytesRedactor.SecretMask);
        maskWritten = true;
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // Nothing sent changes what is logged: every later received byte stays withheld.
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        // No byte is held back: the mask went out when the first withheld byte arrived.
    }
}
