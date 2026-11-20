using System.Buffers;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The <see cref="ILineRedactionRule"/> of the FTP control connection: it writes the argument of
/// <c>PASS</c> and of <c>ACCT</c> as <see cref="LineBytesRedactor.SecretMask"/>, keeping the
/// command word as the client wrote it, and logs every other line unchanged, <c>USER</c>
/// included (ADR-0182, decisions 1 and 6).
/// </summary>
/// <remarks>
/// The command is matched without regard to case. A <c>PASS</c> or <c>ACCT</c> with no argument
/// holds no secret and is logged as it is. FTPS's <c>AUTH TLS</c> upgrade changes nothing: the
/// logged control bytes are plaintext either way.
/// </remarks>
internal sealed class FtpLineRedactionRule : ILineRedactionRule
{
    private const byte Space = (byte)' ';

    /// <inheritdoc/>
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (CarriesSecret(line))
        {
            output.Write(line[..5]);
            output.Write(LineBytesRedactor.SecretMask);
        }
        else
        {
            output.Write(line);
        }
    }

    /// <inheritdoc/>
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // FTP needs no knowledge of what the server sent: the secret is always the argument of PASS or ACCT.
    }

    private static bool CarriesSecret(ReadOnlySpan<byte> line) =>
        line.Length > 5
        && line[4] == Space
        && (IsCommand(line[..4], "pass"u8) || IsCommand(line[..4], "acct"u8));

    private static bool IsCommand(ReadOnlySpan<byte> word, ReadOnlySpan<byte> lowerCaseCommand)
    {
        for (var index = 0; index < word.Length; index++)
        {
            if ((word[index] | 0x20) != lowerCaseCommand[index])
            {
                return false;
            }
        }

        return true;
    }
}