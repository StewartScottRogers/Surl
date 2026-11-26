using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// What the proxy's redactor logs of a client's <c>CONNECT</c> tunnel, as text. A turn starting
/// <c>S:</c> is what the proxy sends the client; any other turn is what the client sends.
/// </summary>
internal static class HttpProxyTunnelRedactionTestKit
{
    public const string Connect = "CONNECT origin.test:21 HTTP/1.1\r\nHost: origin.test:21\r\n\r\n";

    public const string Established = "HTTP/1.1 200 Connection established\r\n\r\n";

    private const string FromProxy = "S:";

    /// <summary>The log of a <c>CONNECT</c>, its <c>200</c>, then the turns.</summary>
    public static string LoggedTunnel(params string[] turns) => Logged([Connect, FromProxy + Established, .. turns]);

    /// <summary>The same, with each of the client's turns arriving one byte at a time.</summary>
    public static string LoggedTunnelByteByByte(params string[] turns) =>
        LoggedTunnel(
        [
            .. turns.SelectMany(turn => turn.StartsWith(FromProxy, StringComparison.Ordinal)
                ? [turn]
                : turn.Select(character => character.ToString())),
        ]);

    /// <summary>The log of the turns on a new connection to the proxy.</summary>
    public static string Logged(params string[] turns)
    {
        var redactor = new HttpProxyProtocolServer(new InMemoryOriginConnector())
            .CreateRedactor(new ExchangeRedactionContext(1, "http"));
        var output = new ArrayBufferWriter<byte>();
        foreach (var turn in turns)
        {
            if (turn.StartsWith(FromProxy, StringComparison.Ordinal))
            {
                redactor.ObserveSent(Encoding.Latin1.GetBytes(turn[FromProxy.Length..]));
            }
            else
            {
                redactor.Redact(Encoding.Latin1.GetBytes(turn), output);
            }
        }

        redactor.Flush(output);

        return Encoding.Latin1.GetString(output.WrittenSpan);
    }
}
