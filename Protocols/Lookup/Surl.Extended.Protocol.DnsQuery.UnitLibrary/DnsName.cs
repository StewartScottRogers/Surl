using System.Text;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A domain name as a sequence of labels, each kept byte for byte with its case (RFC 4343
/// section 4.1). Construction refuses an empty label, a label over 63 bytes and a name over 255
/// bytes on the wire (RFC 1035 section 2.3.4), so every <see cref="DnsName"/> can be written.
/// </summary>
public sealed class DnsName
{
    /// <summary>The longest label, in bytes (RFC 1035 section 2.3.4).</summary>
    public const int MaxLabelLength = 63;

    /// <summary>The longest name on the wire, length bytes and the root's zero included.</summary>
    public const int MaxWireLength = 255;

    private readonly byte[][] labels;

    /// <summary>Creates the name made of <paramref name="labels"/>, leftmost first.</summary>
    /// <exception cref="ArgumentException">
    /// A label is empty or longer than 63 bytes, or the name is longer than 255 bytes on the wire.
    /// </exception>
    public DnsName(IEnumerable<byte[]> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        this.labels = labels.Select(label => label.ToArray()).ToArray();
        WireLength = 1 + this.labels.Sum(label => 1 + label.Length);

        if (this.labels.Any(label => label.Length is 0 or > MaxLabelLength))
        {
            throw new ArgumentException("A label holds 1 to 63 bytes.", nameof(labels));
        }

        if (WireLength > MaxWireLength)
        {
            throw new ArgumentException("A name holds at most 255 bytes on the wire.", nameof(labels));
        }
    }

    /// <summary>The root name, which has no label.</summary>
    public static DnsName Root { get; } = new([]);

    /// <summary>The labels, leftmost first.</summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> Labels =>
        labels.Select(label => new ReadOnlyMemory<byte>(label)).ToArray();

    /// <summary>The name's length uncompressed on the wire, the root's zero byte included.</summary>
    public int WireLength { get; }

    /// <summary>
    /// Returns the name written as dot-separated ASCII labels, with or without the root's
    /// trailing dot; <c>""</c> and <c>"."</c> are the root. No escape is read: a master file's
    /// <c>\X</c> and <c>\DDD</c> forms are the zone reader's.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The text holds a character outside ASCII, or a label or the name is out of bounds.
    /// </exception>
    public static DnsName Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!Ascii.IsValid(text))
        {
            throw new ArgumentException("A name is written in ASCII.", nameof(text));
        }

        var withoutRoot = text.EndsWith('.') ? text[..^1] : text;

        return withoutRoot.Length == 0
            ? Root
            : new DnsName(withoutRoot.Split('.').Select(Encoding.ASCII.GetBytes));
    }

    /// <summary>
    /// Returns the name as dot-separated labels with the root's trailing dot, each byte shown as
    /// the Latin-1 character of the same value.
    /// </summary>
    public override string ToString() =>
        labels.Length == 0
            ? "."
            : string.Concat(labels.Select(label => Encoding.Latin1.GetString(label) + "."));

    /// <summary>The leftmost label at <paramref name="index"/>.</summary>
    internal byte[] LabelAt(int index) => labels[index];

    /// <summary>The number of labels.</summary>
    internal int LabelCount => labels.Length;

    /// <summary>
    /// Returns a key naming, byte for byte, the suffix that starts at label
    /// <paramref name="index"/>: two suffixes have one key exactly when their wire forms match.
    /// </summary>
    internal string SuffixKey(int index) =>
        string.Concat(labels.Skip(index).Select(label => (char)label.Length + Encoding.Latin1.GetString(label)));

    /// <summary>
    /// A key two names share exactly when they are equal ignoring ASCII case (RFC 4343 section 3):
    /// only the bytes A to Z are folded, every other byte compared as it is.
    /// </summary>
    internal string FoldedKey => string.Create(
        SuffixKey(0).Length,
        SuffixKey(0),
        static (span, key) =>
        {
            for (var index = 0; index < key.Length; index++)
            {
                span[index] = char.IsAsciiLetterUpper(key[index]) ? (char)(key[index] | 0x20) : key[index];
            }
        });

    /// <summary>Whether the first label is the single byte <c>*</c>, which makes the name a wildcard (RFC 4592 section 2.1.1).</summary>
    internal bool IsWildcard => labels.Length > 0 && labels[0] is [(byte)'*'];

    /// <summary>Returns the name made of the labels from <paramref name="index"/> on: its ancestor <paramref name="index"/> levels up.</summary>
    internal DnsName Suffix(int index) => new(labels.Skip(index));

    /// <summary>Returns this name's labels followed by <paramref name="origin"/>'s, as a relative name is completed.</summary>
    /// <exception cref="ArgumentException">The name made is longer than 255 bytes on the wire.</exception>
    internal DnsName Append(DnsName origin) => new(labels.Concat(origin.labels));

    /// <summary>Returns <c>*</c> followed by this name's labels: the wildcard whose parent this name is.</summary>
    internal DnsName WildcardChild() => new(new[] { "*"u8.ToArray() }.Concat(labels));

    /// <summary>Whether this name is <paramref name="ancestor"/> or below it, ignoring ASCII case.</summary>
    internal bool IsAtOrBelow(DnsName ancestor) =>
        LabelCount >= ancestor.LabelCount && Suffix(LabelCount - ancestor.LabelCount).FoldedKey == ancestor.FoldedKey;
}
