using System.Buffers;
using System.Text;
using Surl.LineProtocol;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

/// <summary>
/// The <see cref="ILineRedactionRule"/> of a POP3 connection: it writes the argument of <c>PASS</c>
/// as <see cref="LineBytesRedactor.SecretMask"/>, keeping the command word as the client wrote it,
/// and leaves <c>AUTH</c> initial responses and the client lines that answer a <c>+ </c>
/// continuation to a <see cref="SaslLineRedactionRule"/> (ADR-0183). Every other line is logged
/// unchanged: <c>USER</c>, <c>APOP</c> (a digest, not the password; ADR-0182, decision 8) and
/// every maildrop command.
/// </summary>
/// <remarks>
/// <c>PASS</c> is matched without regard to case; with no argument it holds no secret and is
/// logged as it is. A <c>PASS</c> line that answers a pending continuation is masked whole by the
/// SASL rule, which is the same bytes as a masked password.
/// </remarks>
internal sealed class Pop3LineRedactionRule : ILineRedactionRule
{
    private const byte Space = (byte)' ';
    private const int PassWordLength = 4;

    private readonly SaslLineRedactionRule saslRule = new(["AUTH"], "+ ");

    /// <inheritdoc/>
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (IsPassWithArgument(line))
        {
            // A pending continuation still has to be consumed by the SASL rule.
            saslRule.Redact([], output);
            output.Write(line[..(PassWordLength + 1)]);
            output.Write(LineBytesRedactor.SecretMask);
            return;
        }

        saslRule.Redact(line, output);
    }

    /// <inheritdoc/>
    public void ObserveSent(ReadOnlySpan<byte> sent) => saslRule.ObserveSent(sent);

    private static bool IsPassWithArgument(ReadOnlySpan<byte> line) =>
        line.Length > PassWordLength + 1
        && line[PassWordLength] == Space
        && Ascii.EqualsIgnoreCase(line[..PassWordLength], "PASS"u8);
}
