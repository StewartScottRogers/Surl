using System.Buffers;

namespace Surl.Composition;

/// <summary>
/// The exchange-log redactor of a server that withholds nothing: every received byte is logged as it came.
/// </summary>
internal sealed class PassThroughRedactor : Surl.Protocol.Abstractions.IReceivedBytesRedactor
{
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output) => BuffersExtensions.Write(output, received);

    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
    }

    public void Flush(ArrayBufferWriter<byte> output)
    {
    }
}
