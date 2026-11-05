using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The XMPP client-to-server service for the <c>xmpp</c> scheme the Curl port adds (ADR-0083;
/// ADR-0114), judged by RFC 6120, never by the Curl port.
/// </summary>
/// <remarks>
/// <para>
/// Each connection must send its stream header within the head timeout. The header is checked in
/// this order (decision 1): the stream namespace and the default namespace <c>jabber:client</c>
/// (else <c>invalid-namespace</c>), the <c>to</c> domain (else <c>host-unknown</c>; none opens a
/// stream for the first domain served), and a major version of 1 (else
/// <c>unsupported-version</c>). The answer is <c>&lt;?xml version='1.0'?&gt;</c> and a response
/// header with <c>from</c> the stream's domain, a new 32-hex-digit <c>id</c>, <c>version='1.0'</c>
/// and the client's <c>xml:lang</c> or <c>en</c>, then <c>&lt;stream:features/&gt;</c>.
/// <c>&lt;/stream:stream&gt;</c> from the client is answered with the server's, then the connection
/// is closed.
/// </para>
/// <para>
/// Every refusal is a stream error with no text, then <c>&lt;/stream:stream&gt;</c> and a close,
/// preceded by a response header when none was sent (decision 6); <see cref="XmppStreamReader"/>
/// lists what the XML itself is refused for. No header within the head timeout, or a limit
/// cancelling the exchange, is <c>connection-timeout</c>; shutdown is <c>system-shutdown</c>.
/// STARTTLS and <c>xmpps</c> come with BL-537, SASL and binding with BL-538, and stanzas with
/// BL-539; until then a stanza is <c>not-authorized</c>, another element in <c>jabber:client</c> is
/// <c>unsupported-stanza-type</c>, and any other element is <c>policy-violation</c>.
/// </para>
/// </remarks>
/// <param name="options">The mail domains and the random source; <see cref="XmppServerOptions.Default"/> when null.</param>
public sealed class XmppServer(XmppServerOptions? options = null) : IConnectionProtocolServer, IConnectionRefusalWriter
{
    private readonly XmppServerOptions options = options ?? XmppServerOptions.Default;

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["xmpp"]);

    /// <inheritdoc/>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return new XmppServerConnection(connection, context, options).ServeAsync();
    }

    /// <summary>
    /// Answers a connection past <c>--max-connections</c> or <c>--max-connections-per-address</c>
    /// with a response header, the stream error <c>resource-constraint</c> and the close, before
    /// the client's header is read (ADR-0114 decisions 6 and 7). The header's <c>from</c> is the
    /// local address the connection arrived on, as an IP literal, and is left out on a Unix socket.
    /// </summary>
    /// <param name="connection">The refused connection.</param>
    /// <param name="refusal">Which limit refused it; both are answered alike.</param>
    /// <param name="cancellationToken">Ends the write.</param>
    /// <returns>A task that completes when the refusal is written.</returns>
    public async ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var from = connection.LocalEndPoint is IPEndPoint local ? XmppDomains.Literal(local.Address) : null;
        var id = options.RandomSource.NextHexDigits(XmppStreamText.StreamIdHexDigits);
        var text = XmppStreamText.ResponseHeader(from, id, XmppStreamText.DefaultLanguage) +
            XmppStreamText.ErrorAndClose(XmppStreamErrors.ResourceConstraint);
        await connection.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }
}
