using System.Text;

namespace Surl.Protocol.Imap;

/// <summary>
/// One header field of a message or body part (RFC 5322, section 2.2), as stored.
/// </summary>
/// <param name="Name">The field name before the colon; empty for a line that has none.</param>
/// <param name="Raw">The field's bytes as stored: every line of it, folding and line ends kept.</param>
/// <param name="Value">The value after the colon, unfolded (every CR and LF removed) and without
/// the white space at either end.</param>
internal sealed record ImapHeaderField(string Name, ReadOnlyMemory<byte> Raw, byte[] Value)
{
    /// <summary>
    /// The value read as UTF-8, invalid bytes replaced.
    /// </summary>
    public string Text => Encoding.UTF8.GetString(Value);

    /// <summary>
    /// Reads a field from its bytes.
    /// </summary>
    /// <param name="raw">The field's lines as stored.</param>
    /// <returns>The field.</returns>
    public static ImapHeaderField Read(ReadOnlyMemory<byte> raw)
    {
        var span = raw.Span;
        var colon = span.IndexOf((byte)':');
        var name = colon < 0 ? string.Empty : Encoding.ASCII.GetString(span[..colon]).TrimEnd(' ', '\t');
        var value = colon < 0 ? [] : span[(colon + 1)..].ToArray().Where(next => next is not ((byte)'\r' or (byte)'\n')).ToArray();
        return new ImapHeaderField(name, raw, value.AsSpan().Trim(" \t"u8).ToArray());
    }

    /// <summary>
    /// The first field named <paramref name="name"/>, compared without regard to case.
    /// </summary>
    /// <param name="fields">A header's fields.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The field, or <see langword="null"/> when there is none.</returns>
    public static ImapHeaderField? Find(IReadOnlyList<ImapHeaderField> fields, string name) =>
        fields.FirstOrDefault(field => field.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
