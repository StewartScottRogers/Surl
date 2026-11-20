using System.Buffers;
using Surl.Http2;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Http;

/// <summary>
/// The redactor of one http or https connection (ADR-0182, decision 7): HTTP/1.x request heads
/// are redacted by <see cref="HttpRequestHeadBytesRedactor"/>, and the connection moves to
/// <see cref="Http2RequestBytesRedactor"/> when it becomes HTTP/2 - on the client preface as its
/// first bytes (prior knowledge, or ALPN <c>h2</c>), or after the server sent
/// <c>101 Switching Protocols</c> for <c>Upgrade: h2c</c>.
/// </summary>
internal sealed class HttpConnectionBytesRedactor : IReceivedBytesRedactor
{
    private static readonly byte[] SwitchingProtocolsPrefix = "HTTP/1.1 101"u8.ToArray();

    private readonly HttpRequestHeadBytesRedactor http1 = new();
    private readonly Http2RequestBytesRedactor http2 = new();
    private readonly List<byte> undecided = [];
    private bool decidedFirstBytes;
    private bool upgradeAnswered;
    private bool isHttp2;

    /// <inheritdoc />
    public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        if (!decidedFirstBytes)
        {
            DecideFromFirstBytes(received, output);

            return;
        }

        if (upgradeAnswered && !isHttp2)
        {
            http1.Flush(output);
            isHttp2 = true;
        }

        Current.Redact(received, output);
    }

    /// <inheritdoc />
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        if (!isHttp2 && sent.StartsWith(SwitchingProtocolsPrefix))
        {
            upgradeAnswered = true;
        }

        Current.ObserveSent(sent);
    }

    /// <inheritdoc />
    public void Flush(ArrayBufferWriter<byte> output)
    {
        if (!decidedFirstBytes)
        {
            Decide(false, output);
        }

        Current.Flush(output);
    }

    private IReceivedBytesRedactor Current => isHttp2 ? http2 : http1;

    // The preface starts "PRI * HTTP/2.0"; no HTTP/1.x method does, so the first bytes tell them apart.
    private void DecideFromFirstBytes(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
    {
        undecided.AddRange(received.ToArray());
        var preface = Http2ConnectionPreface.Preface;
        var compared = Math.Min(undecided.Count, preface.Length);
        for (var index = 0; index < compared; index++)
        {
            if (undecided[index] != preface[index])
            {
                Decide(false, output);

                return;
            }
        }

        if (undecided.Count >= preface.Length)
        {
            Decide(true, output);
        }
    }

    private void Decide(bool http2Preface, ArrayBufferWriter<byte> output)
    {
        decidedFirstBytes = true;
        isHttp2 = http2Preface;
        var held = undecided.ToArray();
        undecided.Clear();
        Current.Redact(held, output);
    }
}
