namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Splits an inline command into its arguments as Redis's inline parser does (ADR-0106 decision
/// 2): white space separates arguments; <c>"…"</c> quotes with the escapes <c>\n</c>, <c>\r</c>,
/// <c>\t</c>, <c>\b</c>, <c>\a</c> and <c>\xHH</c> (any other escaped byte stands for itself); and
/// <c>'…'</c> quotes with only <c>\'</c>. A closing quote must be followed by white space or the
/// end of the line.
/// </summary>
internal static class RedisInlineCommandSplitter
{
    /// <summary>
    /// The arguments of <paramref name="line"/> (without its line ending), none for a blank line,
    /// or <see langword="null"/> for unbalanced quotes.
    /// </summary>
    public static List<byte[]>? Split(ReadOnlySpan<byte> line)
    {
        var arguments = new List<byte[]>();
        var position = 0;

        while (true)
        {
            while (position < line.Length && IsSpace(line[position]))
            {
                position++;
            }

            if (position == line.Length)
            {
                return arguments;
            }

            var argument = new List<byte>();

            if (!ReadArgument(line, ref position, argument))
            {
                return null;
            }

            arguments.Add([.. argument]);
        }
    }

    private static bool ReadArgument(ReadOnlySpan<byte> line, ref int position, List<byte> argument)
    {
        while (position < line.Length && !IsArgumentEnd(line[position]))
        {
            var current = line[position++];

            switch (current)
            {
                case (byte)'"':
                    return ReadDoubleQuoted(line, ref position, argument);
                case (byte)'\'':
                    return ReadSingleQuoted(line, ref position, argument);
                default:
                    argument.Add(current);
                    break;
            }
        }

        return true;
    }

    private static bool ReadDoubleQuoted(ReadOnlySpan<byte> line, ref int position, List<byte> argument)
    {
        while (position < line.Length)
        {
            var current = line[position++];

            if (current == '"')
            {
                return EndsArgument(line, position);
            }

            if (current == '\\' && position < line.Length)
            {
                position = ReadEscape(line, position, argument);
            }
            else
            {
                argument.Add(current);
            }
        }

        return false;
    }

    private static int ReadEscape(ReadOnlySpan<byte> line, int position, List<byte> argument)
    {
        if (line[position] == 'x' && position + 2 < line.Length
            && IsHexDigit(line[position + 1]) && IsHexDigit(line[position + 2]))
        {
            argument.Add(Convert.FromHexString([(char)line[position + 1], (char)line[position + 2]])[0]);
            return position + 3;
        }

        argument.Add(line[position] switch
        {
            (byte)'n' => (byte)'\n',
            (byte)'r' => (byte)'\r',
            (byte)'t' => (byte)'\t',
            (byte)'b' => (byte)'\b',
            (byte)'a' => (byte)'\a',
            var other => other,
        });
        return position + 1;
    }

    private static bool ReadSingleQuoted(ReadOnlySpan<byte> line, ref int position, List<byte> argument)
    {
        while (position < line.Length)
        {
            var current = line[position++];

            if (current == '\\' && position < line.Length && line[position] == '\'')
            {
                argument.Add((byte)'\'');
                position++;
            }
            else if (current == '\'')
            {
                return EndsArgument(line, position);
            }
            else
            {
                argument.Add(current);
            }
        }

        return false;
    }

    private static bool EndsArgument(ReadOnlySpan<byte> line, int position) =>
        position == line.Length || IsSpace(line[position]);

    private static bool IsSpace(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\n' or 0x0B or 0x0C or (byte)'\r';

    private static bool IsArgumentEnd(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r';

    private static bool IsHexDigit(byte value) => char.IsAsciiHexDigit((char)value);
}
