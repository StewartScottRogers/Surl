using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// An <see cref="IConnectionProtocolServer"/> that also implements <see cref="IExchangeBytesRedactingServer"/>:
/// its redactor writes <c>secret</c> as <c>****</c>, holds back a last line that is not yet whole until
/// the next chunk or the flush, and records what was sent and which exchanges asked for a redactor.
/// </summary>
internal sealed class FakeRedactingConnectionServer(
    Func<IConnection, ExchangeContext, Task> serve, params string[] schemes) : IConnectionProtocolServer, IExchangeBytesRedactingServer
{
    public IReadOnlyList<string> Schemes { get; } = schemes;

    public List<ExchangeRedactionContext> Requests { get; } = [];

    public List<string> Sent { get; } = [];

    public Task ServeAsync(IConnection connection, ExchangeContext context) => serve(connection, context);

    public IReceivedBytesRedactor CreateRedactor(ExchangeRedactionContext context)
    {
        Requests.Add(context);

        return new SecretToStarsRedactor(Sent);
    }

    private sealed class SecretToStarsRedactor(List<string> sent) : IReceivedBytesRedactor
    {
        private string held = string.Empty;

        public void Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)
        {
            var text = held + Encoding.ASCII.GetString(received);
            var cut = text.LastIndexOf('\n') + 1;
            held = text[cut..];
            output.Write(Encoding.ASCII.GetBytes(text[..cut].Replace("secret", "****", StringComparison.Ordinal)));
        }

        public void ObserveSent(ReadOnlySpan<byte> sentBytes) => sent.Add(Encoding.ASCII.GetString(sentBytes));

        public void Flush(ArrayBufferWriter<byte> output)
        {
            output.Write(Encoding.ASCII.GetBytes(held.Replace("secret", "****", StringComparison.Ordinal)));
            held = string.Empty;
        }
    }
}
