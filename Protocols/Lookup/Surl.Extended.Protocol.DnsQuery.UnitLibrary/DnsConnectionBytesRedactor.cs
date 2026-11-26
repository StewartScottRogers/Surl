using System.Buffers;
using Surl.Http2;
using Surl.HttpServing;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// The redactor of one <c>dns</c> connection (ADR-0182; BL-1469). The first byte tells the transports apart:
/// DNS over TCP and TLS starts with a two-byte message length whose first byte is never an ASCII capital
/// letter for a real query, so those bytes pass through as received; an HTTP request starts with a method
/// (a capital letter), so DNS over HTTPS is redacted as HTTP/1.1 request heads, or, when the bytes open with
/// the HTTP/2 preface, as HTTP/2 header blocks. Both HTTP forms withhold the fields of
/// <see cref="HttpWithheldHeaderNames.All"/>: credentials, API keys and session tokens.
/// </summary>
public sealed class DnsConnectionBytesRedactor : IReceivedBytesRedactor
{
    private readonly List<byte> undecided = [];
    private IReceivedBytesRedactor? chosen;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (chosen is not null)
        {
            chosen.Redact(received, output);

            return;
        }

        undecided.AddRange(received.ToArray());
        Decide(output, endOfInput: false);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent) => chosen?.ObserveSent(sent);

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (chosen is null)
        {
            Decide(output, endOfInput: true);
        }

        chosen?.Flush(output);
    }

    private void Decide(ArrayBufferWriter<byte> output, bool endOfInput)
    {
        if (undecided.Count == 0)
        {
            return;
        }

        if (undecided[0] is not (>= (byte)'A' and <= (byte)'Z'))
        {
            Choose(new PassThroughRedactor(), output);

            return;
        }

        var preface = Http2ConnectionPreface.Preface;
        var compared = Math.Min(undecided.Count, preface.Length);
        for (var index = 0; index < compared; index++)
        {
            if (undecided[index] != preface[index])
            {
                Choose(HttpExchangeRedaction.CreateRedactor(), output);

                return;
            }
        }

        if (undecided.Count >= preface.Length)
        {
            Choose(new Http2RequestBytesRedactor(HttpWithheldHeaderNames.ApiKeysAndSessionTokens), output);
        }
        else if (endOfInput)
        {
            Choose(HttpExchangeRedaction.CreateRedactor(), output);
        }
    }

    private void Choose(IReceivedBytesRedactor redactor, ArrayBufferWriter<byte> output)
    {
        chosen = redactor;
        var held = undecided.ToArray();
        undecided.Clear();
        redactor.Redact(held, output);
    }

    private sealed class PassThroughRedactor : IReceivedBytesRedactor
    {
        public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output) => output.Write(received);

        public void ObserveSent(ReadOnlySpan<byte> sent)
        {
        }

        public void Flush(ArrayBufferWriter<byte> output)
        {
        }
    }
}
