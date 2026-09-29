namespace Surl.Protocol.Http;

/// <summary>
/// Builds one request head from its lines, fed one at a time: empty lines before the
/// request line are skipped (RFC 9112, section 2.2), then the request line, then header
/// field lines until the empty line that ends the head.
/// </summary>
internal sealed class HttpRequestHeadLineReader
{
    private readonly List<HttpRequestField> fields = [];
    private string? method;
    private string requestTarget = string.Empty;
    private Version version = new(0, 0);

    /// <summary>
    /// Whether the request line has been accepted, so only empty lines have been fed when
    /// it is <see langword="false"/>.
    /// </summary>
    public bool HasRequestLine => method is not null;

    /// <summary>
    /// Takes the next line of the head.
    /// </summary>
    /// <param name="line">The line up to its LF, without the LF; a CR before the LF is removed here.</param>
    /// <returns>
    /// <see langword="null"/> while the head needs more lines; otherwise the finished head,
    /// or the failure that ends it.
    /// </returns>
    public HttpRequestHeadReadResult? AcceptLine(ReadOnlySpan<byte> line)
    {
        var content = HttpSyntax.WithoutTrailingCarriageReturn(line);

        return method is null ? AcceptRequestLine(content) : AcceptFieldLine(content);
    }

    private HttpRequestHeadReadResult? AcceptRequestLine(ReadOnlySpan<byte> content)
    {
        if (content.IsEmpty)
        {
            return null;
        }

        var failure = HttpRequestLineParser.Parse(content, out var parsedMethod, out requestTarget, out version);
        if (failure is { } outcome)
        {
            return HttpRequestHeadReadResult.NoHead(outcome);
        }

        method = parsedMethod;

        return null;
    }

    private HttpRequestHeadReadResult? AcceptFieldLine(ReadOnlySpan<byte> content)
    {
        if (content.IsEmpty)
        {
            return HttpRequestHeadReadResult.Read(new HttpRequestHead(method!, requestTarget, version, fields));
        }

        var failure = HttpFieldLineParser.Parse(content, out var field);
        if (failure is { } outcome)
        {
            return HttpRequestHeadReadResult.NoHead(outcome);
        }

        fields.Add(field!);

        return null;
    }
}
