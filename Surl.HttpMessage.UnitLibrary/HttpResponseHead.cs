using System.Text;

namespace Surl.HttpMessage;

/// <summary>
/// A response head being built: a status line naming the protocol's highest version, such as
/// <c>HTTP/1.1</c> or <c>RTSP/1.0</c>, and header fields in the order added (RFC 9112,
/// sections 4 and 5; RFC 2326, section 7).
/// </summary>
/// <remarks>
/// The status line always names the highest version the server speaks, whatever minor version
/// the request named (RFC 9110, section 2.5).
/// </remarks>
public sealed class HttpResponseHead
{
    /// <summary>
    /// The value of every response's <c>Server</c> field: the name alone, with no surl, .NET
    /// or operating-system version (ADR-0006, section 3).
    /// </summary>
    public const string ServerName = "surl";

    private readonly List<KeyValuePair<string, string>> fields = [];

    /// <summary>
    /// Starts an <c>HTTP/1.1</c> head with <paramref name="status"/> and no fields.
    /// </summary>
    /// <param name="status">The status the head carries.</param>
    public HttpResponseHead(HttpStatus status)
        : this(HttpMessageProtocol.Http11, status)
    {
    }

    /// <summary>
    /// Starts a head whose status line names <paramref name="protocol"/>'s highest version,
    /// with <paramref name="status"/> and no fields.
    /// </summary>
    /// <param name="protocol">The protocol whose status line the head writes.</param>
    /// <param name="status">The status the head carries.</param>
    public HttpResponseHead(HttpMessageProtocol protocol, HttpStatus status)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(status);

        Protocol = protocol;
        Status = status;
    }

    /// <summary>
    /// The protocol whose status line the head writes.
    /// </summary>
    public HttpMessageProtocol Protocol { get; }

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
    /// Appends one <c>WWW-Authenticate</c> field per value, in order (ADR-0032, section 6).
    /// </summary>
    /// <param name="values">The challenge values; empty adds nothing.</param>
    /// <returns>This head.</returns>
    public HttpResponseHead AddChallengeFields(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        foreach (var value in values)
        {
            AddField("WWW-Authenticate", value);
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
        text.Append(Protocol.HighestVersionText).Append(' ').Append(Status.Code).Append(' ').Append(Status.ReasonPhrase).Append("\r\n");

        foreach (var field in fields)
        {
            text.Append(field.Key).Append(": ").Append(field.Value).Append("\r\n");
        }

        text.Append("\r\n");

        return Encoding.Latin1.GetBytes(text.ToString());
    }
}
