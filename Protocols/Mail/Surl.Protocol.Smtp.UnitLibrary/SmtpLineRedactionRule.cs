using System.Buffers;
using Surl.LineProtocol;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The <see cref="ILineRedactionRule"/> of one SMTP exchange: it withholds the <c>AUTH</c> initial
/// response and every answer to a <c>334 </c> continuation through <see cref="SaslLineRedactionRule"/>,
/// and logs the message lines between a <c>354</c> reply and the terminating <c>.</c> line as they
/// arrived, because message content is not a credential (ADR-0183).
/// </summary>
internal sealed class SmtpLineRedactionRule : ILineRedactionRule
{
    private static readonly string[] AuthCommandWords = ["AUTH"];

    private readonly SaslLineRedactionRule sasl = new(AuthCommandWords, "334 ");
    private bool isReadingMessage;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (isReadingMessage)
        {
            isReadingMessage = !line.SequenceEqual("."u8);
            output.Write(line);
            return;
        }

        sasl.Redact(line, output);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        if (sent.StartsWith("354"u8))
        {
            isReadingMessage = true;
        }

        sasl.ObserveSent(sent);
    }
}
