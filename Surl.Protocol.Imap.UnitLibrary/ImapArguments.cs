using System.Text;

namespace Surl.Protocol.Imap;

/// <summary>
/// Reads one command's tag, name and arguments by RFC 3501 section 9's grammar: atoms, quoted
/// strings, synchronizing literals and parenthesised lists, one after another, from the left
/// (ADR-0055, decision 3). Bytes of 0x80 and above are atom characters, since curl sends a URL's
/// UTF-8 bytes unquoted (ADR-0055, decision 6).
/// </summary>
internal sealed class ImapArguments
{
    private readonly ImapCommandText text;
    private int lineIndex;
    private int position;

    /// <summary>
    /// Starts reading <paramref name="text"/> at its first byte.
    /// </summary>
    /// <param name="text">The command as it arrived.</param>
    public ImapArguments(ImapCommandText text)
    {
        this.text = text;
    }

    /// <summary>
    /// Whether every byte and literal of the command has been read.
    /// </summary>
    public bool IsAtEnd => position == Line.Length && lineIndex == text.Literals.Count;

    private byte[] Line => text.Lines[lineIndex];

    private bool IsAtLiteral => position == Line.Length && lineIndex < text.Literals.Count;

    /// <summary>
    /// The tag at the start of <paramref name="line"/>: one or more <c>ASTRING-CHAR</c>s of
    /// 7-bit ASCII other than <c>+</c>, followed by a space or the line's end.
    /// </summary>
    /// <param name="line">A command's first line.</param>
    /// <returns>The tag, or <see langword="null"/> when the line starts with none.</returns>
    public static string? ReadTagOf(byte[] line) => new ImapArguments(new ImapCommandText([line], [])).ReadTag();

    /// <summary>
    /// Reads the tag, as <see cref="ReadTagOf"/> describes it.
    /// </summary>
    /// <returns>The tag, or <see langword="null"/> when there is none.</returns>
    public string? ReadTag()
    {
        var tag = ReadAtom(IsTagChar);
        return tag is not null && (position == Line.Length || Line[position] == ' ') ? Encoding.ASCII.GetString(tag) : null;
    }

    /// <summary>
    /// Reads one space.
    /// </summary>
    /// <returns>Whether a space was next, and read.</returns>
    public bool TryReadSpace()
    {
        if (position < Line.Length && Line[position] == ' ')
        {
            position++;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads an atom: one or more <c>ATOM-CHAR</c>s.
    /// </summary>
    /// <returns>The atom in capitals, or <see langword="null"/> when none is next.</returns>
    public string? ReadAtom() => ReadAtom(IsAtomChar) is { } atom ? Encoding.UTF8.GetString(atom).ToUpperInvariant() : null;

    /// <summary>
    /// Reads an <c>astring</c>: an atom of <c>ASTRING-CHAR</c>s, a quoted string or a literal.
    /// </summary>
    /// <returns>Its bytes, or <see langword="null"/> when none is next.</returns>
    public byte[]? ReadAString() => ReadString() ?? ReadAtom(IsAStringChar);

    /// <summary>
    /// Reads a <c>list-mailbox</c>: an <c>astring</c> whose atom may also hold <c>%</c> and <c>*</c>.
    /// </summary>
    /// <returns>Its bytes, or <see langword="null"/> when none is next.</returns>
    public byte[]? ReadListMailbox() => ReadString() ?? ReadAtom(IsListChar);

    /// <summary>
    /// Reads a space, then an <c>astring</c>.
    /// </summary>
    /// <returns>The <c>astring</c>'s bytes, or <see langword="null"/> when either is not next.</returns>
    public byte[]? ReadSpacedAString() => TryReadSpace() ? ReadAString() : null;

    /// <summary>
    /// Reads a space, then a <c>list-mailbox</c>.
    /// </summary>
    /// <returns>The <c>list-mailbox</c>'s bytes, or <see langword="null"/> when either is not next.</returns>
    public byte[]? ReadSpacedListMailbox() => TryReadSpace() ? ReadListMailbox() : null;

    /// <summary>
    /// Reads a space, then a parenthesised list of atoms.
    /// </summary>
    /// <returns>The atoms in capitals, or <see langword="null"/> when either is not next.</returns>
    public IReadOnlyList<string>? ReadSpacedAtomList() => TryReadSpace() ? ReadAtomList() : null;

    /// <summary>
    /// Reads a parenthesised list of one or more atoms separated by single spaces.
    /// </summary>
    /// <returns>The atoms in capitals, or <see langword="null"/> when no such list is next.</returns>
    public IReadOnlyList<string>? ReadAtomList()
    {
        if (position == Line.Length || Line[position] != '(')
        {
            return null;
        }

        position++;
        List<string> atoms = [];
        do
        {
            if (ReadAtom() is not { } atom)
            {
                return null;
            }

            atoms.Add(atom);
        }
        while (TryReadSpace());

        return TryReadByte((byte)')') ? atoms : null;
    }

    /// <summary>
    /// Reads the atom <paramref name="expected"/>, compared without regard to case, and nothing
    /// when another atom or no atom is next.
    /// </summary>
    /// <param name="expected">The atom in capitals.</param>
    /// <returns>Whether it was next, and read.</returns>
    public bool TryReadAtom(string expected)
    {
        var start = position;
        if (ReadAtom() == expected)
        {
            return true;
        }

        position = start;
        return false;
    }

    /// <summary>
    /// Reads one or more bytes of the line, each one <paramref name="isRunByte"/> accepts, as
    /// ASCII text: a sequence set, a number, or a fetch item's name.
    /// </summary>
    /// <param name="isRunByte">Which bytes the run holds; each must be 7-bit ASCII.</param>
    /// <returns>The run, or <see langword="null"/> when its first byte is not next.</returns>
    public string? ReadRun(Func<byte, bool> isRunByte) =>
        ReadAtom(isRunByte) is { } run ? Encoding.ASCII.GetString(run) : null;

    /// <summary>
    /// Reads one byte of the line.
    /// </summary>
    /// <param name="expected">The byte expected next.</param>
    /// <returns>Whether it was next, and read.</returns>
    public bool TryReadByte(byte expected)
    {
        if (position < Line.Length && Line[position] == expected)
        {
            position++;
            return true;
        }

        return false;
    }

    // ATOM-CHAR (RFC 3501 section 9): any byte but "(" ")" "{" SP CTL "%" "*" DQUOTE "\" "]".
    private static bool IsAtomChar(byte value) =>
        value > 0x20 && value != 0x7F && "(){%*\"\\]"u8.IndexOf(value) < 0;

    private static bool IsAStringChar(byte value) => IsAtomChar(value) || value == ']';

    private static bool IsListChar(byte value) => IsAStringChar(value) || value is (byte)'%' or (byte)'*';

    private static bool IsTagChar(byte value) => value < 0x80 && value != '+' && IsAStringChar(value);

    private byte[]? ReadAtom(Func<byte, bool> isAtomChar)
    {
        var start = position;
        while (position < Line.Length && isAtomChar(Line[position]))
        {
            position++;
        }

        return position == start ? null : Line[start..position];
    }

    private byte[]? ReadString() =>
        IsAtLiteral ? TakeLiteral()
        : position < Line.Length && Line[position] == '"' ? ReadQuoted()
        : null;

    private byte[] TakeLiteral()
    {
        var literal = text.Literals[lineIndex];
        lineIndex++;
        position = 0;
        return literal;
    }

    // A quoted string: DQUOTE, then bytes with "\" before each DQUOTE or "\", then DQUOTE.
    private byte[]? ReadQuoted()
    {
        List<byte> value = [];
        for (var index = position + 1; index < Line.Length; index++)
        {
            var next = Line[index];
            if (next == '"')
            {
                position = index + 1;
                return [.. value];
            }

            if (next == '\\' && !IsQuotedSpecialAt(++index))
            {
                return null;
            }

            value.Add(Line[index]);
        }

        return null;
    }

    private bool IsQuotedSpecialAt(int index) => index < Line.Length && Line[index] is (byte)'"' or (byte)'\\';
}
