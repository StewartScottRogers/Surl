using System.Text;

namespace Surl.Protocol.Http;

/// <summary>
/// A response head being built: an <c>HTTP/1.1</c> status line and header fields in the
/// order added (RFC 9112, sections 4 and 5).
/// </summary>
/// <remarks>
/// The status line always names HTTP/1.1, the highest version the server speaks, whatever
/// minor version the request named (RFC 9110, section 2.5).
/// </remarks>
internal sealed class HttpResponseHead
{
    /// <summary>
    /// The value of every response's <c>Server</c> field: the name alone, with no surl, .NET
    /// or operating-system version (ADR-0006, section 3).
    /// </summary>
    public const string ServerName = "surl";

    private readonly List<KeyValuePair<string, string>> fields = [];

    /// <summary>
    /// Starts a head with <paramref name="status"/> and no fields.
    /// </summary>
    /// <param name="status">The status the head carries.</param>
    public HttpResponseHead(HttpStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        Status = status;
    }

    /// <summary>
    /// The status the head carries.
    /// </summary>
    public HttpStatus Status { get; }

    /// <summary>
    /// Appends a field.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The field value; <see langword="null"/> adds nothing.</param>
    /// <returns>This head.</returns>
    public HttpResponseHead AddField(string name, string? value)
    {
        if (value is not null)
        {
            fields.Add(new KeyValuePair<string, string>(name, value));
        }

        return this;
    }

    /// <summary>
    /// Returns the head as sent: the status line, each field, then the empty line, every
    /// line ended by CRLF, one byte per character (Latin-1).
    /// </summary>
    /// <returns>The head's bytes.</returns>
    public byte[] ToBytes()
    {
        var text = new StringBuilder();
        text.Append("HTTP/1.1 ").Append(Status.Code).Append(' ').Append(Status.ReasonPhrase).Append("\r\n");

        foreach (var field in fields)
        {
            text.Append(field.Key).Append(": ").Append(field.Value).Append("\r\n");
        }

        text.Append("\r\n");

        return Encoding.Latin1.GetBytes(text.ToString());
    }
}
