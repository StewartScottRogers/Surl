using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Writes one inline Redis command line (<c>AUTH s3cret</c>) as it is logged (ADR-0182): the
/// password of <c>AUTH</c> and of <c>HELLO ... AUTH user password</c> becomes
/// <see cref="LineBytesRedactor.SecretMask"/>, the user name stays, and every other line is
/// written as sent.
/// </summary>
internal static class RedisInlineCommandRedaction
{
    private const byte Space = (byte)' ';

    /// <summary>
    /// Writes <paramref name="line"/>, with no line ending, to <paramref name="output"/>.
    /// </summary>
    /// <param name="line">The line, without its line ending.</param>
    /// <param name="output">Where the logged form goes.</param>
    public static void Write(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        var arguments = RedisInlineCommandSplitter.Split(line);
        if (arguments is null)
        {
            WriteUnreadable(line, output);
            return;
        }

        var (first, last) = SecretRange(arguments);
        if (first < 0)
        {
            output.Write(line);
            return;
        }

        for (var index = 0; index < arguments.Count; index++)
        {
            if (index > 0)
            {
                output.Write([Space]);
            }

            output.Write(index >= first && index <= last ? LineBytesRedactor.SecretMask : arguments[index]);
        }
    }

    // Unbalanced quotes: the server refuses the line, but the word after AUTH or HELLO may still
    // be a password, so everything after the command word is withheld.
    private static void WriteUnreadable(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        var trimmed = line.TrimStart(Space);
        var wordLength = trimmed.IndexOf(Space);
        var word = wordLength < 0 ? trimmed : trimmed[..wordLength];
        if (IsCommand(word, "AUTH") || IsCommand(word, "HELLO"))
        {
            output.Write(word);
            output.Write(" "u8);
            output.Write(LineBytesRedactor.SecretMask);
            return;
        }

        output.Write(line);
    }

    // The inclusive range of arguments that are secret, or (-1, -1).
    private static (int First, int Last) SecretRange(List<byte[]> arguments)
    {
        if (arguments.Count == 0)
        {
            return (-1, -1);
        }

        if (IsCommand(arguments[0], "AUTH"))
        {
            return arguments.Count switch
            {
                1 => (-1, -1),
                3 => (2, 2),
                _ => (1, arguments.Count - 1),
            };
        }

        return IsCommand(arguments[0], "HELLO") ? HelloSecretRange(arguments) : (-1, -1);
    }

    private static (int First, int Last) HelloSecretRange(List<byte[]> arguments)
    {
        var authIndex = arguments.FindIndex(1, argument => IsCommand(argument, "AUTH"));
        var password = authIndex + 2;
        return authIndex > 0 && password < arguments.Count ? (password, password) : (-1, -1);
    }

    private static bool IsCommand(ReadOnlySpan<byte> word, string name) =>
        Ascii.EqualsIgnoreCase(word, Encoding.ASCII.GetBytes(name));
}
