using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// The <see cref="ILineRedactionRule"/> for an inline Redis <c>AUTH</c> line in a tunnel that
/// <see cref="SaslLineRedactionRule"/> cannot read: one whose command word is quoted, or that holds a
/// vertical tab, form feed or carriage return, which Redis counts as white space (ADR-0183, amendment
/// 8). It writes the line up to the byte that ends the command word, then
/// <see cref="LineBytesRedactor.SecretMask"/> and nothing more. Every other line is written as
/// received.
/// </summary>
/// <remarks>
/// The command word is the first run of bytes that are not white space. It is quoted <c>AUTH</c> when
/// removing its <c>"</c> and <c>'</c> bytes leaves <c>AUTH</c>, matched without regard to case; a word
/// holding both <c>"</c> and a backslash is taken for it too, because an escape may spell any of its
/// letters. One instance serves one tunnel.
/// </remarks>
public sealed class TunnelRedisAuthLineRule : ILineRedactionRule
{
    private static ReadOnlySpan<byte> WhiteSpace => " \t\n\v\f\r"u8;

    // The white space SaslLineRedactionRule does not split words at; a line feed never reaches this rule.
    private static ReadOnlySpan<byte> UnreadSeparators => "\v\f\r"u8;

    private static ReadOnlySpan<byte> Quotes => "\"'"u8;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var maskStart = FindMaskStart(line);
        if (maskStart < 0)
        {
            output.Write(line);
            return;
        }

        output.Write(line[..maskStart]);
        output.Write(LineBytesRedactor.SecretMask);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // The secret is always on the line that names it, whatever the origin sent.
    }

    // Just after the byte that ends the command word; -1 when this rule withholds nothing of the line.
    private static int FindMaskStart(ReadOnlySpan<byte> line)
    {
        // Leading white space is not part of the command word; a line of nothing else leaves it empty.
        var fromCommand = line.TrimStart(WhiteSpace);
        var commandLength = fromCommand.IndexOfAny(WhiteSpace);
        if (commandLength < 0 || !fromCommand[commandLength..].ContainsAnyExcept(WhiteSpace))
        {
            return -1;
        }

        var command = fromCommand[..commandLength];
        var withheld = line.ContainsAny(UnreadSeparators)
            ? IsAuth(command) || MayBeQuotedAuth(command)
            : MayBeQuotedAuth(command);

        return withheld ? line.Length - fromCommand.Length + commandLength + 1 : -1;
    }

    private static bool IsAuth(ReadOnlySpan<byte> word) => Ascii.EqualsIgnoreCase(word, "AUTH"u8);

    private static bool MayBeQuotedAuth(ReadOnlySpan<byte> word)
    {
        if (!word.ContainsAny(Quotes))
        {
            return false;
        }

        if (word.Contains((byte)'"') && word.Contains((byte)'\\'))
        {
            return true;
        }

        byte[] unquoted = [.. word.ToArray().Where(value => !Quotes.Contains(value))];

        return IsAuth(unquoted);
    }
}
