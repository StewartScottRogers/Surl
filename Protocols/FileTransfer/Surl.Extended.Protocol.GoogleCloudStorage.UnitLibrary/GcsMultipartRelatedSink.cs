using System.Text;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The write-only stream a <c>multipart/related</c> body (RFC 2387) is copied into (ADR-0148, decision 7): exactly two
/// parts, the first JSON object metadata held in memory up to <see cref="MaxMetadataBytes"/>, the second the object's
/// bytes passed on to <c>content</c> as they arrive. Parsed by hand with bounded buffers: the part headers are held up to
/// <see cref="MaxPartHeaderBytes"/> and the search for a delimiter holds one delimiter's length of the bytes before it.
/// </summary>
/// <remarks>
/// A body that breaks the grammar - bytes before the first delimiter, a third part, a close after one part - is
/// <see cref="GcsMultipartRelatedProblem.Malformed"/>; a first part past the limit is
/// <see cref="GcsMultipartRelatedProblem.MetadataTooLarge"/>. After a problem the rest of the body is read and dropped.
/// A body that ends before the closing delimiter is not <see cref="IsComplete"/>.
/// </remarks>
internal sealed class GcsMultipartRelatedSink : Stream
{
    /// <summary>
    /// The most bytes the metadata part may have (ADR-0148, decision 8).
    /// </summary>
    public const int MaxMetadataBytes = 1024 * 1024;

    /// <summary>
    /// The most bytes of header fields a part may have.
    /// </summary>
    public const int MaxPartHeaderBytes = 8192;

    /// <summary>
    /// The longest boundary the grammar allows (RFC 2046, section 5.1.1).
    /// </summary>
    public const int MaxBoundaryLength = 70;

    private static readonly byte[] HeaderEnd = "\r\n\r\n"u8.ToArray();

    private readonly Func<byte[], CancellationToken, Task<Stream?>> openContent;
    private readonly byte[] delimiter;
    private readonly MemoryStream metadata = new();
    private readonly MemoryStream partHeaders = new();
    private Stream? content;
    private byte[] held = [];
    private State state = State.Opening;
    private int partsStarted;

    /// <summary>
    /// Starts a reader for a body whose parts are separated by <paramref name="boundary"/>.
    /// </summary>
    /// <param name="boundary">The boundary parameter of the <c>Content-Type</c>, at most <see cref="MaxBoundaryLength"/> characters.</param>
    /// <param name="openContent">Called once the second part's header fields are read, with the first part's bytes, to open where the second part's bytes go;
    /// it answers <see langword="null"/> to refuse the upload, which ends the reading as <see cref="GcsMultipartRelatedProblem.Refused"/>.</param>
    public GcsMultipartRelatedSink(string boundary, Func<byte[], CancellationToken, Task<Stream?>> openContent)
    {
        this.openContent = openContent;
        delimiter = Encoding.ASCII.GetBytes($"\r\n--{boundary}");

        // The first delimiter has no line break before it, so the body is read as if one came first.
        held = "\r\n"u8.ToArray();
    }

    private enum State
    {
        Opening,
        AfterDelimiter,
        PartHeaders,
        Part,
        Closed,
    }

    /// <summary>
    /// What went wrong with the body so far, or <see langword="null"/>.
    /// </summary>
    public GcsMultipartRelatedProblem? Problem { get; private set; }

    /// <summary>
    /// Whether the closing delimiter of the second part came, with no problem found.
    /// </summary>
    public bool IsComplete => Problem is null && state == State.Closed;

    /// <summary>
    /// The first part, the object's JSON metadata.
    /// </summary>
    public byte[] MetadataPart => metadata.ToArray();

    /// <summary>
    /// The <c>Content-Type</c> of the second part, or <see langword="null"/> when it names none.
    /// </summary>
    public string? ContentPartType { get; private set; }

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Reads the boundary out of a <c>Content-Type</c> of <c>multipart/related</c>.
    /// </summary>
    /// <param name="contentType">The field's value, or <see langword="null"/>.</param>
    /// <returns>The boundary, or <see langword="null"/> when the type is another, or has no boundary or one that is empty, too long or has a control character.</returns>
    public static string? BoundaryOf(string? contentType)
    {
        if (contentType is null)
        {
            return null;
        }

        var parameters = contentType.Split(';', StringSplitOptions.TrimEntries);
        if (!parameters[0].Equals("multipart/related", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var boundary = parameters.Skip(1)
            .Where(parameter => parameter.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
            .Select(parameter => parameter["boundary=".Length..].Trim('"'))
            .FirstOrDefault();

        return boundary is { Length: > 0 and <= MaxBoundaryLength } && boundary.All(character => character is > ' ' and < '\u007f')
            ? boundary
            : null;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>
    /// Not supported: bytes are copied asynchronously, never by a blocking write.
    /// </summary>
    /// <param name="buffer">Unused.</param>
    /// <param name="offset">Unused.</param>
    /// <param name="count">Unused.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (Problem is not null || state == State.Closed)
        {
            return;
        }

        var data = new byte[held.Length + buffer.Length];
        held.CopyTo(data, 0);
        buffer.CopyTo(data.AsMemory(held.Length));
        held = [];
        await ConsumeAsync(data, cancellationToken);
    }

    // Each state eats what it can of the bytes and hands back what is left; a state that needs more bytes holds them.
    private async Task ConsumeAsync(byte[] data, CancellationToken cancellationToken)
    {
        var start = 0;
        while (Problem is null && state != State.Closed && start < data.Length)
        {
            start = state switch
            {
                State.Opening => ConsumeOpening(data, start),
                State.AfterDelimiter => ConsumeAfterDelimiter(data, start),
                State.PartHeaders => await ConsumePartHeadersAsync(data, start, cancellationToken),
                _ => await ConsumePartAsync(data, start, cancellationToken),
            };
        }
    }

    // Nothing may come before the first delimiter: the body begins with it.
    private int ConsumeOpening(byte[] data, int start)
    {
        var rest = data.AsSpan(start);
        if (rest.Length < delimiter.Length)
        {
            held = rest.ToArray();

            return data.Length;
        }

        if (!rest.StartsWith(delimiter))
        {
            Problem = GcsMultipartRelatedProblem.Malformed;

            return data.Length;
        }

        state = State.AfterDelimiter;

        return start + delimiter.Length;
    }

    // After a delimiter come "--" (the end) or a line break (a part); two bytes are needed to tell.
    private int ConsumeAfterDelimiter(byte[] data, int start)
    {
        var rest = data.AsSpan(start);
        if (rest.Length < 2)
        {
            held = rest.ToArray();

            return data.Length;
        }

        if (rest.StartsWith("--"u8))
        {
            // The parts must be exactly two; what follows the closing delimiter is an epilogue, ignored.
            state = State.Closed;
            Problem = partsStarted == 2 ? null : GcsMultipartRelatedProblem.Malformed;

            return data.Length;
        }

        if (!rest.StartsWith("\r\n"u8) || partsStarted == 2)
        {
            Problem = GcsMultipartRelatedProblem.Malformed;

            return data.Length;
        }

        partsStarted++;
        partHeaders.SetLength(0);

        // The line break that ended the delimiter line also begins the header section, so a part with no fields ends it at once.
        partHeaders.Write("\r\n"u8);
        state = State.PartHeaders;

        return start + 2;
    }

    // The header fields end at an empty line; they are held, bounded, to read the second part's Content-Type.
    private async Task<int> ConsumePartHeadersAsync(byte[] data, int start, CancellationToken cancellationToken)
    {
        var previousLength = (int)partHeaders.Length;
        partHeaders.Write(data.AsSpan(start));
        var end = partHeaders.GetBuffer().AsSpan(0, (int)partHeaders.Length).IndexOf(HeaderEnd);
        if (end < 0)
        {
            if (partHeaders.Length > MaxPartHeaderBytes)
            {
                Problem = GcsMultipartRelatedProblem.Malformed;
            }

            return data.Length;
        }

        var consumedFromData = end + HeaderEnd.Length - previousLength;
        if (end + HeaderEnd.Length > MaxPartHeaderBytes)
        {
            Problem = GcsMultipartRelatedProblem.Malformed;

            return data.Length;
        }

        if (partsStarted == 2)
        {
            ContentPartType = ContentTypeOf(Encoding.Latin1.GetString(partHeaders.GetBuffer(), 0, end));
            content = await openContent(metadata.ToArray(), cancellationToken);
            if (content is null)
            {
                Problem = GcsMultipartRelatedProblem.Refused;

                return data.Length;
            }
        }

        state = State.Part;

        return start + consumedFromData;
    }

    // The part's bytes up to the next delimiter go to their place; the last delimiter-length less one are held, for a delimiter cut across two writes.
    private async Task<int> ConsumePartAsync(byte[] data, int start, CancellationToken cancellationToken)
    {
        var rest = data.AsMemory(start);
        var found = rest.Span.IndexOf(delimiter);
        var safe = found >= 0 ? found : Math.Max(0, rest.Length - (delimiter.Length - 1));
        if (!await PassOnAsync(rest[..safe], cancellationToken))
        {
            return data.Length;
        }

        if (found < 0)
        {
            held = rest[safe..].ToArray();

            return data.Length;
        }

        state = State.AfterDelimiter;

        return start + found + delimiter.Length;
    }

    // The first part is the metadata, the second the object's bytes; false once the metadata is past its limit.
    private async Task<bool> PassOnAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (partsStarted == 2)
        {
            await content!.WriteAsync(bytes, cancellationToken);

            return true;
        }

        metadata.Write(bytes.Span);
        if (metadata.Length > MaxMetadataBytes)
        {
            Problem = GcsMultipartRelatedProblem.MetadataTooLarge;
        }

        return Problem is null;
    }

    // The value of the first Content-Type field among the part's header lines, or null.
    private static string? ContentTypeOf(string headers) =>
        headers.Split("\r\n")
            .Where(line => line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line["Content-Type:".Length..].Trim())
            .FirstOrDefault();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            metadata.Dispose();
            partHeaders.Dispose();
        }

        base.Dispose(disposing);
    }
}
