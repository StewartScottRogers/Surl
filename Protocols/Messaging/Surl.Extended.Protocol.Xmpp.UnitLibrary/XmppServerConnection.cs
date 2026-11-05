using System.Text;
using System.Xml.Linq;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// One XMPP connection (ADR-0114 decisions 1, 2, 6 and 7): the client's stream header within the
/// head timeout, the response header and the features of the connection's phase, then first-level
/// elements until the client closes the stream, the input ends, a stream error ends it, or
/// <c>&lt;starttls/&gt;</c> upgrades the connection and the client opens a new stream.
/// </summary>
internal sealed class XmppServerConnection
{
    /// <summary>How long the server tries to write its farewell once the exchange was cancelled for a limit or shutdown.</summary>
    public static readonly TimeSpan FarewellWriteDeadline = TimeSpan.FromSeconds(1);

    /// <summary>The SASL mechanisms an XMPP stream may offer, in the order it offers them (ADR-0114 decision 3).</summary>
    private static readonly string[] OfferableMechanisms = ["SCRAM-SHA-256", "SCRAM-SHA-1", "PLAIN", "ANONYMOUS"];

    private static readonly XName StartTls = XNamespace.Get(XmppNamespaces.Tls) + "starttls";

    private static readonly XName Auth = XNamespace.Get(XmppNamespaces.Sasl) + "auth";

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly XmppServerOptions options;
    private readonly IReadOnlyList<string> domains;
    private XmppStreamReader reader;
    private bool headerSent;
    private bool connectionUnusable;

    public XmppServerConnection(IConnection connection, ExchangeContext context, XmppServerOptions options)
    {
        this.connection = connection;
        this.context = context;
        this.options = options;
        reader = new XmppStreamReader(connection, context.Limits.MaxMessageBytes);
        domains = XmppDomains.Served(context.ListenUrl, context.LocalEndPoint, options.MailDomains);
    }

    private enum ElementOutcome
    {
        Continue,
        RestartAfterTls,
        EndStream,
    }

    // Decision 2: before TLS on xmpp with a certificate, unless --allow-plaintext-auth.
    private bool IsTlsRequired =>
        connection.TlsSession is null && options.IsTlsUpgradeAvailable && !options.AllowPlaintextAuthentication;

    public async Task ServeAsync()
    {
        try
        {
            await ServeStreamsAsync();
            if (!connectionUnusable)
            {
                await connection.CompleteWritesAsync(context.CancellationToken);
            }
        }
        catch (XmppStreamErrorException error)
        {
            context.Log.Note($"{error.Message}; answered the stream error {error.Condition} and closed.");
            await WriteErrorAsync(error.Condition, context.CancellationToken);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            await SayFarewellAsync();
        }
        catch (IOException failure)
        {
            context.Log.Note($"The connection failed ({failure.Message}).");
        }
    }

    // Each stream after the first is the one the client opens after TLS (RFC 6120 5.4.3.3).
    private async Task ServeStreamsAsync()
    {
        var header = await ReadHeaderWithinHeadTimeoutAsync(upgradeFirst: false);
        while (header is not null)
        {
            header = await ServeStreamAsync(header);
        }
    }

    // One stream: its header answered, then its elements; returns the next stream's header when
    // the connection was upgraded and the client opened one, else null.
    private async Task<XmppStreamHeader?> ServeStreamAsync(XmppStreamHeader header)
    {
        await OpenStreamAsync(header);
        if (header.IsEmptyElement)
        {
            await WriteAsync(XmppStreamText.Close);
            return null;
        }

        while (await reader.ReadElementAsync(context.CancellationToken) is { } element)
        {
            var outcome = await AnswerAsync(element);
            if (outcome != ElementOutcome.Continue)
            {
                return outcome == ElementOutcome.RestartAfterTls ? await ReadHeaderWithinHeadTimeoutAsync(upgradeFirst: true) : null;
            }
        }

        if (reader.ClientClosedStream)
        {
            context.Log.Note("The client closed the stream; closed.");
            await WriteAsync(XmppStreamText.Close);
        }

        return null;
    }

    // Decision 7: the head timeout runs from accept, or from <proceed/>, to a complete stream
    // header, the TLS handshake included.
    private async Task<XmppStreamHeader?> ReadHeaderWithinHeadTimeoutAsync(bool upgradeFirst)
    {
        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        if (upgradeFirst && !await UpgradedToTlsAsync(cancellation.Token))
        {
            return null;
        }

        try
        {
            return await reader.ReadHeaderAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            throw new XmppStreamErrorException(XmppStreamErrors.ConnectionTimeout, "No stream header arrived within the head timeout");
        }
    }

    // RFC 6120 5.4.3.2: a handshake that fails, or is cut off, leaves no TLS to write in, so the
    // connection closes with no bytes. After it a new XML parser reads the new stream, and any
    // byte the old one had buffered past <starttls/> is thrown away (5.4.3.3).
    private async Task<bool> UpgradedToTlsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var session = await connection.UpgradeToTlsAsync(cancellationToken);
            context.Log.Note($"Upgraded to {session.Protocol}.");
            reader = new XmppStreamReader(connection, context.Limits.MaxMessageBytes);
            return true;
        }
        catch (TlsHandshakeException failure)
        {
            connectionUnusable = true;
            context.Log.Note($"The TLS handshake failed ({failure.Message}); closed.");
            return false;
        }
        catch (OperationCanceledException)
        {
            connectionUnusable = true;
            context.Log.Note("The TLS handshake did not finish; closed.");
            return false;
        }
    }

    // Decision 1: the domain, then the version, then the response header and features.
    private async Task OpenStreamAsync(XmppStreamHeader header)
    {
        var domain = StreamDomain(header.To);
        RequireVersion1(header.Version);
        var language = string.IsNullOrEmpty(header.Language) ? XmppStreamText.DefaultLanguage : header.Language;
        await WriteHeaderAsync(domain, language);
        await WriteAsync(Features());
        context.Log.Note($"Opened a stream for {domain}.");
    }

    // Decision 2's table: STARTTLS alone while it is required; otherwise voluntary STARTTLS while
    // the connection could still be upgraded, then the SASL mechanisms for the TLS state.
    private string Features()
    {
        if (IsTlsRequired)
        {
            return XmppStreamText.Features(XmppStreamText.StartTlsRequired);
        }

        var startTls = connection.TlsSession is null && options.IsTlsUpgradeAvailable ? XmppStreamText.StartTlsOffered : string.Empty;
        return XmppStreamText.Features(startTls + XmppStreamText.Mechanisms(OfferedMechanisms()));
    }

    private string[] OfferedMechanisms()
    {
        var offered = options.AuthenticationPolicy.GetSaslMechanisms(new SaslOfferRequest(context.Scheme, connection.TlsSession));
        return [.. OfferableMechanisms.Where(offered.Contains)];
    }

    // Decision 2: <starttls/> is served; <auth/> while TLS is required is refused and the stream
    // kept. SASL (BL-538) and stanzas (BL-539) are not served yet, so anything else ends the stream.
    private async Task<ElementOutcome> AnswerAsync(XElement element)
    {
        if (element.Name == StartTls)
        {
            return await AnswerStartTlsAsync();
        }

        if (element.Name == Auth && IsTlsRequired)
        {
            context.Log.Note("A SASL <auth/> arrived before TLS while TLS is required; answered encryption-required.");
            await WriteAsync(XmppStreamText.EncryptionRequired);
            return ElementOutcome.Continue;
        }

        throw Refusal(element);
    }

    // RFC 6120 5.4.2: <proceed/> and the upgrade when the server has a certificate and the
    // connection is not yet TLS; otherwise the TLS <failure/>, the close and no more (5.4.2.2).
    private async Task<ElementOutcome> AnswerStartTlsAsync()
    {
        if (connection.TlsSession is null && options.IsTlsUpgradeAvailable)
        {
            context.Log.Note("Answered <starttls/> with <proceed/>.");
            await WriteAsync(XmppStreamText.Proceed);
            return ElementOutcome.RestartAfterTls;
        }

        context.Log.Note(connection.TlsSession is null
            ? "A <starttls/> arrived and no certificate is configured; answered the TLS failure and closed."
            : "A <starttls/> arrived on a connection already TLS; answered the TLS failure and closed.");
        await WriteAsync(XmppStreamText.TlsFailureAndClose);
        return ElementOutcome.EndStream;
    }

    private string StreamDomain(string? to)
    {
        var domain = to is null ? domains.FirstOrDefault() : XmppDomains.Normalized(to);
        return domain is not null && domains.Contains(domain)
            ? domain
            : throw new XmppStreamErrorException(XmppStreamErrors.HostUnknown, $"The stream header's to named {to ?? "no domain"}, and no such domain is served");
    }

    // RFC 6120 4.7.5: a major version of 1 opens a 1.0 stream, the lower of the two; no version,
    // or another major version, is a version this server does not speak (4.9.3.25).
    private static void RequireVersion1(string? version)
    {
        if (!IsMajorVersion1(version))
        {
            throw new XmppStreamErrorException(
                XmppStreamErrors.UnsupportedVersion, $"The stream header's version was {version ?? "missing"}");
        }
    }

    // "major.minor", two non-negative integers (4.7.5), the major one 1.
    private static bool IsMajorVersion1(string? version) =>
        version is not null && version.All(character => char.IsAsciiDigit(character) || character == '.') &&
        Version.TryParse(version, out var parsed) && parsed.Build < 0 && parsed.Major == 1;

    // Until SASL (BL-538) and stanzas (BL-539) are served, no other first-level element is: a
    // stanza is not authorized (5.3.1 before TLS, 4.9.3.12), any other element in jabber:client is
    // not a stanza type, and any other element is not allowed in this phase.
    private static XmppStreamErrorException Refusal(XElement element)
    {
        var name = element.Name;
        if (name.NamespaceName != XmppNamespaces.Client)
        {
            return new(XmppStreamErrors.PolicyViolation, $"The element {name} is not allowed before authentication");
        }

        return name.LocalName is "message" or "presence" or "iq"
            ? new(XmppStreamErrors.NotAuthorized, $"A {name.LocalName} stanza arrived before authentication")
            : new(XmppStreamErrors.UnsupportedStanzaType, $"The element {name.LocalName} is not a stanza");
    }

    // ADR-0006 section 5: the idle timeout or maximum duration is connection-timeout, shutdown is
    // system-shutdown, each written on a short deadline of the server's own.
    private async Task SayFarewellAsync()
    {
        var condition = context.IsCancelledForALimit ? XmppStreamErrors.ConnectionTimeout : XmppStreamErrors.SystemShutdown;
        context.Log.Note($"The exchange was cancelled; answered the stream error {condition} and closed.");
        using var deadline = new CancellationTokenSource(FarewellWriteDeadline, context.TimeProvider);
        try
        {
            await WriteErrorAsync(condition, deadline.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    // RFC 6120 4.9.1.1: an error before the response header is preceded by one.
    private async Task WriteErrorAsync(string condition, CancellationToken cancellationToken)
    {
        try
        {
            if (!headerSent)
            {
                await WriteHeaderAsync(domains.FirstOrDefault(), XmppStreamText.DefaultLanguage, cancellationToken);
            }

            await WriteAsync(XmppStreamText.ErrorAndClose(condition), cancellationToken);
            await connection.CompleteWritesAsync(cancellationToken);
        }
        catch (IOException failure)
        {
            context.Log.Note($"The connection failed ({failure.Message}).");
        }
    }

    private Task WriteHeaderAsync(string domain, string language) => WriteHeaderAsync(domain, language, context.CancellationToken);

    private async Task WriteHeaderAsync(string? domain, string language, CancellationToken cancellationToken)
    {
        headerSent = true;
        var id = options.RandomSource.NextHexDigits(XmppStreamText.StreamIdHexDigits);
        await WriteAsync(XmppStreamText.ResponseHeader(domain, id, language), cancellationToken);
    }

    private Task WriteAsync(string text) => WriteAsync(text, context.CancellationToken);

    private async Task WriteAsync(string text, CancellationToken cancellationToken) =>
        await connection.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
}
