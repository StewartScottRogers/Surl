using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.HttpMessage;

/// <summary>
/// The <see cref="ILineRedactionRule"/> for the lines of one HTTP/1.x or RTSP request head: it
/// masks the credentials of the secret fields, notes the framing fields, and says when the blank
/// line that ends the head has gone by (ADR-0182, decisions 1 and 7).
/// </summary>
internal sealed class HttpRequestHeadLineRule : ILineRedactionRule
{
    private readonly HashSet<string> secretFieldNames;
    private readonly List<HttpRequestField> framingFields = [];
    private int lineCount;
    private bool previousFieldWasSecret;
    private int minorVersion = 1;

    /// <summary>
    /// Creates a rule for <see cref="HttpWithheldHeaderNames.Credentials"/> and any further fields.
    /// </summary>
    /// <param name="additionalSecretFieldNames">Fields whose whole value is a secret, such as an API key header.</param>
    public HttpRequestHeadLineRule(IEnumerable<string> additionalSecretFieldNames)
    {
        secretFieldNames = new HashSet<string>(HttpWithheldHeaderNames.Credentials, StringComparer.OrdinalIgnoreCase);
        secretFieldNames.UnionWith(additionalSecretFieldNames);
    }

    /// <summary>
    /// Whether the blank line that ends the head has been seen since the last <see cref="StartNextHead"/>.
    /// </summary>
    public bool HeadIsComplete { get; private set; }

    /// <summary>
    /// Whether no line of a head has been read since the last <see cref="StartNextHead"/>.
    /// </summary>
    public bool NoHeadLineRead => lineCount == 0;

    /// <summary>
    /// The framing the finished head declares.
    /// </summary>
    public HttpRequestBodyFraming Framing =>
        HttpRequestBodyFraming.Of(new HttpRequestHead("", "", new Version(1, minorVersion), framingFields));

    /// <summary>
    /// Forgets the finished head so the next bytes are read as a new request head.
    /// </summary>
    public void StartNextHead()
    {
        HeadIsComplete = false;
        lineCount = 0;
        previousFieldWasSecret = false;
        minorVersion = 1;
        framingFields.Clear();
    }

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        if (line.IsEmpty)
        {
            HeadIsComplete = lineCount > 0;
            return;
        }

        lineCount++;
        if (lineCount == 1)
        {
            minorVersion = line[^1] == (byte)'0' ? 0 : 1;
            output.Write(line);
        }
        else if (line[0] is (byte)' ' or (byte)'\t')
        {
            RedactContinuation(line, output);
        }
        else
        {
            RedactField(line, output);
        }
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
    }

    private void RedactContinuation(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        if (!previousFieldWasSecret)
        {
            output.Write(line);
            return;
        }

        int leading = line.Length - line.TrimStart(" \t"u8).Length;
        output.Write(line[..leading]);
        output.Write(LineBytesRedactor.SecretMask);
    }

    private void RedactField(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        int colon = line.IndexOf((byte)':');
        if (colon < 0)
        {
            previousFieldWasSecret = false;
            output.Write(line);
            return;
        }

        string name = Encoding.Latin1.GetString(line[..colon].TrimEnd(" \t"u8));
        ReadOnlySpan<byte> afterColon = line[(colon + 1)..];
        ReadOnlySpan<byte> value = afterColon.Trim(" \t"u8);
        if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
        {
            framingFields.Add(new HttpRequestField(name, Encoding.Latin1.GetString(value)));
        }

        previousFieldWasSecret = secretFieldNames.Contains(name);
        if (!previousFieldWasSecret || value.IsEmpty)
        {
            output.Write(line);
            return;
        }

        int valueStart = line.Length - afterColon.Length + (afterColon.Length - afterColon.TrimStart(" \t"u8).Length);
        output.Write(line[..valueStart]);
        WriteMaskedValue(name, value, output);
    }

    private static void WriteMaskedValue(string name, ReadOnlySpan<byte> value, ArrayBufferWriter<byte> output)
    {
        int space = value.IndexOf((byte)' ');
        bool isCredentialField = HttpWithheldHeaderNames.Credentials.Contains(name, StringComparer.OrdinalIgnoreCase);
        if (isCredentialField && space > 0)
        {
            output.Write(value[..(space + 1)]);
        }

        output.Write(LineBytesRedactor.SecretMask);
    }
}
