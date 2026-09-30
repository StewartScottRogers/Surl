using System.Text;

namespace Surl.Protocol.Ftp;

/// <summary>
/// One FTP command line split into its command and its argument (ADR-0052, decision 1).
/// </summary>
/// <param name="Command">
/// The first word, up to the first space, in upper case so it matches without regard to case;
/// empty for a blank line, and <c>?</c> for a word holding anything but printable ASCII, which
/// names no command.
/// </param>
/// <param name="Argument">
/// The bytes after that one space, as sent; <see langword="null"/> when the line has no space
/// or nothing after it, which is a command sent without an argument.
/// </param>
internal sealed record FtpCommandLine(string Command, byte[]? Argument)
{
    private const string UnnamedCommand = "?";

    /// <summary>
    /// Splits one command line.
    /// </summary>
    /// <param name="line">The line's bytes, without its line ending.</param>
    /// <returns>The command and its argument.</returns>
    public static FtpCommandLine Split(byte[] line)
    {
        var space = Array.IndexOf(line, (byte)' ');
        var word = space < 0 ? line.AsSpan() : line.AsSpan(0, space);
        byte[]? argument = space < 0 || space == line.Length - 1 ? null : line[(space + 1)..];

        return new FtpCommandLine(NameCommand(word), argument);
    }

    private static string NameCommand(ReadOnlySpan<byte> word)
    {
        foreach (var character in word)
        {
            if (character is < 0x21 or > 0x7E)
            {
                return UnnamedCommand;
            }
        }

        return Encoding.ASCII.GetString(word).ToUpperInvariant();
    }
}
