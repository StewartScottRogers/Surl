using System.Text;

namespace Surl.Protocol.Imap;

/// <summary>
/// A message, or one part of one, read from its stored bytes by RFC 5322 and RFC 2045/2046
/// structure (ADR-0055, decision 4): its header and fields, its body, its media type, and the
/// parts a multipart holds or the message a <c>message/rfc822</c> part holds.
/// </summary>
/// <remarks>
/// A multipart with no boundary, or whose body holds no part, is read as one <c>text/plain</c>
/// part. Parts are read at most <see cref="MaxDepth"/> levels deep; a part below that is read as
/// a leaf, so no message can make the reading recurse without end.
/// </remarks>
internal sealed class ImapBodyPart
{
    /// <summary>
    /// How many levels of multipart and <c>message/rfc822</c> nesting are read.
    /// </summary>
    public const int MaxDepth = 32;

    private ImapBodyPart(ReadOnlyMemory<byte> entity, int headerLength, IReadOnlyList<ImapHeaderField> fields, ImapContentType type)
    {
        Entity = entity;
        Header = entity[..headerLength];
        Body = entity[headerLength..];
        Fields = fields;
        Type = type;
    }

    /// <summary>
    /// The whole entity: its header, then its body.
    /// </summary>
    public ReadOnlyMemory<byte> Entity { get; }

    /// <summary>
    /// The header, with the empty line that ends it when there is one.
    /// </summary>
    public ReadOnlyMemory<byte> Header { get; }

    /// <summary>
    /// The body after the header.
    /// </summary>
    public ReadOnlyMemory<byte> Body { get; }

    /// <summary>
    /// The header's fields, in order.
    /// </summary>
    public IReadOnlyList<ImapHeaderField> Fields { get; }

    /// <summary>
    /// The media type.
    /// </summary>
    public ImapContentType Type { get; }

    /// <summary>
    /// The parts of a multipart, in order; empty for any other type.
    /// </summary>
    public IReadOnlyList<ImapBodyPart> Parts { get; private set; } = [];

    /// <summary>
    /// The message a <c>message/rfc822</c> part holds; otherwise <see langword="null"/>.
    /// </summary>
    public ImapBodyPart? Message { get; private set; }

    /// <summary>
    /// Reads a stored message.
    /// </summary>
    /// <param name="message">The message's bytes.</param>
    /// <returns>The message.</returns>
    public static ImapBodyPart ReadMessage(ReadOnlyMemory<byte> message) => Read(message, ImapContentType.TextPlain, 0);

    /// <summary>
    /// The part a section's part number names, counted from this message: part <c>n</c> of a
    /// multipart is its <c>n</c>th part, a message that is not multipart has the one part 1, its
    /// body, and a <c>message/rfc822</c> part's numbers go on into the message it holds.
    /// </summary>
    /// <param name="part">The part number's levels.</param>
    /// <returns>The part, or <see langword="null"/> when there is no such part.</returns>
    public ImapBodyPart? FindPart(IReadOnlyList<int> part)
    {
        IReadOnlyList<ImapBodyPart> choices = Parts.Count > 0 ? Parts : [this];
        ImapBodyPart? found = null;
        foreach (var level in part)
        {
            found = level <= choices.Count ? choices[level - 1] : null;
            choices = found?.SubParts ?? [];
        }

        return found;
    }

    /// <summary>
    /// The header fields <paramref name="names"/> lists (or, when <paramref name="isExcluding"/>,
    /// every other field), each as stored, followed by an empty line (RFC 3501, section 6.4.5).
    /// </summary>
    /// <param name="names">The field names in capitals.</param>
    /// <param name="isExcluding">Whether the fields listed are the ones left out.</param>
    /// <returns>The fields' bytes and the empty line.</returns>
    public byte[] SelectFields(IReadOnlyList<string> names, bool isExcluding)
    {
        var selected = new MemoryStream();
        foreach (var field in Fields.Where(field => names.Contains(field.Name.ToUpperInvariant()) != isExcluding))
        {
            selected.Write(field.Raw.Span);
            if (!field.Raw.Span.EndsWith("\n"u8))
            {
                selected.Write("\r\n"u8);
            }
        }

        selected.Write("\r\n"u8);
        return selected.ToArray();
    }

    // Part n.m of a part: the parts of a multipart, or of the message a message/rfc822 part holds.
    private IReadOnlyList<ImapBodyPart> SubParts =>
        Parts.Count > 0 ? Parts
        : Message is { } message ? message.FindPartChoices()
        : [];

    private IReadOnlyList<ImapBodyPart> FindPartChoices() => Parts.Count > 0 ? Parts : [this];

    private static ImapBodyPart Read(ReadOnlyMemory<byte> entity, ImapContentType defaultType, int depth)
    {
        var (headerLength, fields) = ReadHeader(entity);
        var declared = ImapContentType.Read(ImapHeaderField.Find(fields, "Content-Type")) ?? defaultType;
        var body = entity[headerLength..];
        var parts = ReadPartsWithinDepth(body, declared, depth);
        var type = TypeRead(declared, parts);
        return new ImapBodyPart(entity, headerLength, fields, type)
        {
            Parts = parts,
            Message = ReadHeldMessage(body, type, depth),
        };
    }

    private static List<ImapBodyPart> ReadPartsWithinDepth(ReadOnlyMemory<byte> body, ImapContentType type, int depth) =>
        type.IsMultipart && depth < MaxDepth ? ReadParts(body, type, depth) : [];

    // A multipart with no part read is one text/plain part.
    private static ImapContentType TypeRead(ImapContentType declared, List<ImapBodyPart> parts) =>
        declared.IsMultipart && parts.Count == 0 ? ImapContentType.TextPlain : declared;

    private static ImapBodyPart? ReadHeldMessage(ReadOnlyMemory<byte> body, ImapContentType type, int depth) =>
        type.IsMessage && depth < MaxDepth ? Read(body, ImapContentType.TextPlain, depth + 1) : null;

    // The header ends at its first empty line, which it keeps; without one, it is everything.
    private static (int Length, List<ImapHeaderField> Fields) ReadHeader(ReadOnlyMemory<byte> entity)
    {
        List<ImapHeaderField> fields = [];
        var fieldStart = -1;
        foreach (var line in ImapTextLine.Split(entity))
        {
            if (line.IsEmpty || !IsContinuation(entity.Span, line, fieldStart))
            {
                AddField(fields, entity, fieldStart, line.Start);
                fieldStart = line.Start;
            }

            if (line.IsEmpty)
            {
                return (line.Next, fields);
            }
        }

        AddField(fields, entity, fieldStart, entity.Length);
        return (entity.Length, fields);
    }

    // A line starting with white space folds into the field before it, when there is one.
    private static bool IsContinuation(ReadOnlySpan<byte> entity, ImapTextLine line, int fieldStart) =>
        fieldStart >= 0 && entity[line.Start] is (byte)' ' or (byte)'\t';

    private static void AddField(List<ImapHeaderField> fields, ReadOnlyMemory<byte> entity, int fieldStart, int fieldEnd)
    {
        if (fieldStart >= 0)
        {
            fields.Add(ImapHeaderField.Read(entity[fieldStart..fieldEnd]));
        }
    }

    // The parts between "--boundary" lines, up to "--boundary--" or the body's end (RFC 2046,
    // section 5.1.1); the line break before each delimiter belongs to the delimiter.
    private static List<ImapBodyPart> ReadParts(ReadOnlyMemory<byte> body, ImapContentType type, int depth)
    {
        var boundary = type.Parameter("BOUNDARY");
        var ranges = string.IsNullOrEmpty(boundary) ? [] : FindParts(body, Encoding.UTF8.GetBytes("--" + boundary));
        var partType = type.Subtype == "DIGEST" ? ImapContentType.MessageRfc822 : ImapContentType.TextPlain;
        return [.. ranges.Select(range => Read(body[range.Start..range.End], partType, depth + 1))];
    }

    private static List<(int Start, int End)> FindParts(ReadOnlyMemory<byte> body, byte[] delimiter)
    {
        List<(int Start, int End)> ranges = [];
        var partStart = -1;
        foreach (var line in ImapTextLine.Split(body).Where(line => IsDelimiter(body.Span[line.Start..line.End], delimiter)))
        {
            if (partStart >= 0)
            {
                ranges.Add((partStart, EndBefore(body.Span, line.Start, partStart)));
            }

            if (body.Span[line.Start..line.End][delimiter.Length..].StartsWith("--"u8))
            {
                return ranges;
            }

            partStart = line.Next;
        }

        if (partStart >= 0)
        {
            ranges.Add((partStart, body.Length));
        }

        return ranges;
    }

    // "--boundary", then "--" or only transport padding.
    private static bool IsDelimiter(ReadOnlySpan<byte> line, byte[] delimiter) =>
        line.StartsWith(delimiter) && (line[delimiter.Length..].StartsWith("--"u8) || line[delimiter.Length..].Trim(" \t"u8).IsEmpty);

    private static int EndBefore(ReadOnlySpan<byte> body, int delimiterStart, int partStart)
    {
        var end = delimiterStart;
        end -= end > partStart && body[end - 1] == '\n' ? 1 : 0;
        end -= end > partStart && body[end - 1] == '\r' ? 1 : 0;
        return end;
    }
}
