using System.Text;
using System.Xml.Linq;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// One XMPP connection (ADR-0114 decisions 1 to 4, 6 and 7): the client's stream header within the
/// head timeout, the response header and the features of the connection's phase, then first-level
/// elements until the client closes the stream, the input ends, or a stream error ends it. After
/// <c>&lt;starttls/&gt;</c> upgrades the connection, and after SASL success, the client opens a new
/// stream on the same connection.
/// </summary>
internal sealed class XmppServerConnection
{
    /// <summary>How long the server tries to write its farewell once the exchange was cancelled for a limit or shutdown.</summary>
    public static readonly TimeSpan FarewellWriteDeadline = TimeSpan.FromSeconds(1);

    /// <summary>The SASL mechanisms an XMPP stream may offer, in the order it offers them (ADR-0114 decision 3).</summary>
    private static readonly string[] OfferableMechanisms = ["SCRAM-SHA-256", "SCRAM-SHA-1", "PLAIN", "ANONYMOUS"];

    private static readonly XName StartTls = XNamespace.Get(XmppNamespaces.Tls) + "starttls";

    private static readonly XName Auth = XNamespace.Get(XmppNamespaces.Sasl) + "auth";

    private static readonly XName Response = XNamespace.Get(XmppNamespaces.Sasl) + "response";

    private static readonly XName Abort = XNamespace.Get(XmppNamespaces.Sasl) + "abort";

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly XmppServerOptions options;
    private readonly IReadOnlyList<string> domains;
    private readonly XmppSaslLogin login;
    private readonly XmppSessionRegistry sessions;
    private readonly Lock writeOrder = new();
    private Task lastWrite = Task.CompletedTask;
    private XmppStreamReader reader;
    private XmppResourceBinding? binding;
    private readonly CancellationTokenSource endRequest = new();
    private readonly CancellationTokenSource readCancellation;
    private readonly CancellationTokenSource loginDeadline;
    private string? endCondition;
    private XmppBoundSession? session;
    private XmppStanzaRouter? router;
    private string[] offeredMechanisms = [];
    private string streamDomain = string.Empty;
    private bool headerSent;
    private bool connectionUnusable;

    public XmppServerConnection(IConnection connection, ExchangeContext context, XmppServerOptions options, XmppSessionRegistry sessions)
    {
        this.connection = connection;
        this.context = context;
        this.options = options;
        this.sessions = sessions;
        reader = new XmppStreamReader(connection, context.Limits.MaxMessageBytes);
        domains = XmppDomains.Served(context.ListenUrl, context.LocalEndPoint, options.MailDomains);
        login = new XmppSaslLogin(options.AuthenticationPolicy, context, options.RandomSource, WriteAsync);
        readCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, endRequest.Token);

        // Decision 7: no resource bound within twice the head timeout of accept is policy-violation.
        var headTimeout = context.Limits.HeadTimeout;
        loginDeadline = new CancellationTokenSource(
            headTimeout == Timeout.InfiniteTimeSpan ? Timeout.InfiniteTimeSpan : headTimeout * 2, context.TimeProvider);
        loginDeadline.Token.Register(() => RequestEnd(XmppStreamErrors.PolicyViolation));
    }

    private enum ElementOutcome
    {
        Continue,
        RestartAfterTls,
        RestartAfterLogin,
        EndStream,
    }

    // Decision 2: before TLS on xmpp with a certificate, unless --allow-plaintext-auth.
    private bool IsTlsRequired =>
        connection.TlsSession is null && options.IsTlsUpgradeAvailable && !options.AllowPlaintextAuthentication;

    public async Task ServeAsync()
    {
        try
        {
            await ServeUntilClosedAsync();
        }
        finally
        {
            if (session is not null)
            {
                sessions.Remove(session);
            }

            loginDeadline.Dispose();
            readCancellation.Dispose();
        }
    }

    // Another stream took over this one's resource (conflict), or the login deadline passed
    // (policy-violation): the read in progress is cut off and the error written.
    private void RequestEnd(string condition)
    {
        endCondition = condition;
        endRequest.Cancel();
    }

    private async Task ServeUntilClosedAsync()
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
        catch (OperationCanceledException) when (endCondition is not null)
        {
            context.Log.Note($"The stream was ended; answered the stream error {endCondition} and closed.");
            await WriteErrorAsync(endCondition, context.CancellationToken);
        }
        catch (IOException failure)
        {
            context.Log.Note($"The connection failed ({failure.Message}).");
        }
    }

    // Each stream after the first is the one the client opens after TLS (RFC 6120 5.4.3.3) or
    // after SASL success (6.4.6).
    private async Task ServeStreamsAsync()
    {
        var header = await ReadHeaderWithinHeadTimeoutAsync(upgradeFirst: false);
        while (header is not null)
        {
            header = await ServeStreamAsync(header);
        }
    }

    // One stream: its header answered, then its elements; returns the next stream's header when
    // the connection was upgraded or logged in and the client opened one, else null. The head
    // timeout runs from <proceed/> or <success/> to the new header (decision 7).
    private async Task<XmppStreamHeader?> ServeStreamAsync(XmppStreamHeader header)
    {
        await OpenStreamAsync(header);
        if (header.IsEmptyElement)
        {
            await WriteAsync(XmppStreamText.Close);
            return null;
        }

        while (await reader.ReadElementAsync(readCancellation.Token) is { } element)
        {
            var outcome = await AnswerAsync(element);
            if (outcome != ElementOutcome.Continue)
            {
                return outcome == ElementOutcome.EndStream
                    ? null
                    : await ReadHeaderWithinHeadTimeoutAsync(upgradeFirst: outcome == ElementOutcome.RestartAfterTls);
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
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(readCancellation.Token, headTimeout.Token);
        if (upgradeFirst && !await UpgradedToTlsAsync(cancellation.Token))
        {
            return null;
        }

        try
        {
            return await reader.ReadHeaderAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (endCondition is null && !context.CancellationToken.IsCancellationRequested)
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
        streamDomain = domain;
        var language = string.IsNullOrEmpty(header.Language) ? XmppStreamText.DefaultLanguage : header.Language;
        await WriteHeaderAsync(domain, language);
        await WriteAsync(Features());
        context.Log.Note($"Opened a stream for {domain}.");
    }

    // Decision 2's table: the bind feature alone once logged in; STARTTLS alone while it is
    // required; otherwise voluntary STARTTLS while the connection could still be upgraded, then
    // the SASL mechanisms for the TLS state.
    private string Features()
    {
        if (binding is not null)
        {
            return XmppStreamText.Features(XmppSaslText.BindFeature);
        }

        if (IsTlsRequired)
        {
            return XmppStreamText.Features(XmppStreamText.StartTlsRequired);
        }

        var startTls = connection.TlsSession is null && options.IsTlsUpgradeAvailable ? XmppStreamText.StartTlsOffered : string.Empty;
        var offered = options.AuthenticationPolicy.GetSaslMechanisms(new SaslOfferRequest(context.Scheme, connection.TlsSession));
        offeredMechanisms = [.. OfferableMechanisms.Where(offered.Contains)];
        return XmppStreamText.Features(startTls + XmppStreamText.Mechanisms(offeredMechanisms));
    }

    // Once logged in, stanzas go to the binding. Before: <starttls/> is served, and SASL's
    // elements (decision 3), except that while TLS is required <auth/> is refused and the stream
    // kept (decision 2). Anything else ends the stream.
    private async Task<ElementOutcome> AnswerAsync(XElement element)
    {
        if (binding is not null)
        {
            await AnswerStanzaAsync(binding, RequireStanza(element));
            return ElementOutcome.Continue;
        }

        if (element.Name == StartTls)
        {
            return await AnswerStartTlsAsync();
        }

        if (element.Name == Auth && IsTlsRequired)
        {
            await login.RefuseBeforeTlsAsync();
            return ElementOutcome.Continue;
        }

        return IsTlsRequired ? throw Refusal(element) : await AnswerSaslAsync(element);
    }

    // Decision 4, then 5: the binding until a resource is bound, then the router, with the stream
    // in the server's registry so other streams can reach it.
    private async Task AnswerStanzaAsync(XmppResourceBinding resourceBinding, XElement stanza)
    {
        if (router is not null)
        {
            await router.AnswerAsync(stanza);
            return;
        }

        await resourceBinding.AnswerAsync(stanza);
        if (session is not null)
        {
            router = new XmppStanzaRouter(session, sessions, domains, context, WriteAsync);
        }
    }

    // Decision 4: the stream joins the registry before its bind result is written, taking over a
    // stream bound to the same resource (conflict) unless the account is at its resource cap.
    private bool TryBind(string resource)
    {
        var candidate = new XmppBoundSession(login.BareJid!, resource, TryDeliverAsync, RequestEnd);
        if (!sessions.TryAdd(candidate, out var displaced))
        {
            return false;
        }

        session = candidate;
        loginDeadline.Dispose();
        displaced?.EndWithError(XmppStreamErrors.Conflict);
        return true;
    }

    // A stanza another stream routes here: a failed or cancelled write is the sender's to note,
    // and this stream finds its connection broken on its own next read or write.
    private async Task<bool> TryDeliverAsync(string stanza)
    {
        try
        {
            await WriteAsync(stanza, context.CancellationToken);
            return true;
        }
        catch (Exception failure) when (failure is IOException or OperationCanceledException)
        {
            return false;
        }
    }

    // RFC 6120 6.4: each SASL element to the login; on success the binding takes over and a new
    // XML parser reads the stream the client restarts (6.4.6), as after TLS.
    private async Task<ElementOutcome> AnswerSaslAsync(XElement element)
    {
        var name = element.Name;
        var answering = name == Auth ? login.StartAsync(element, offeredMechanisms, streamDomain, connection.TlsSession)
            : name == Response ? login.ContinueAsync(element)
            : name == Abort ? login.AbortAsync()
            : throw Refusal(element);
        await answering;
        if (login.BareJid is null)
        {
            return ElementOutcome.Continue;
        }

        binding = new XmppResourceBinding(login.BareJid, context, options.RandomSource, WriteAsync, TryBind);
        reader = new XmppStreamReader(connection, context.Limits.MaxMessageBytes);
        return ElementOutcome.RestartAfterLogin;
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

    // Before login, no other first-level element is served: a stanza is not authorized (5.3.1
    // before TLS, 4.9.3.12), any other element in jabber:client is not a stanza type, and any other
    // element is not allowed in this phase.
    private static XmppStreamErrorException Refusal(XElement element)
    {
        var name = element.Name;
        if (name.NamespaceName != XmppNamespaces.Client)
        {
            return new(XmppStreamErrors.PolicyViolation, $"The element {name} is not allowed before authentication");
        }

        return IsStanza(name)
            ? new(XmppStreamErrors.NotAuthorized, $"A {name.LocalName} stanza arrived before authentication")
            : NotAStanza(name);
    }

    // After login only stanzas are allowed: <starttls/> and <auth/> are policy-violation (decision 3).
    private static XElement RequireStanza(XElement element)
    {
        var name = element.Name;
        if (name.NamespaceName != XmppNamespaces.Client)
        {
            throw new XmppStreamErrorException(XmppStreamErrors.PolicyViolation, $"The element {name} is not allowed after authentication");
        }

        return IsStanza(name) ? element : throw NotAStanza(name);
    }

    private static bool IsStanza(XName name) => name.LocalName is "message" or "presence" or "iq";

    private static XmppStreamErrorException NotAStanza(XName name) =>
        new(XmppStreamErrors.UnsupportedStanzaType, $"The element {name.LocalName} is not a stanza");

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

    // This stream's own answers and stanzas other streams route to it are written one after
    // another, in the order they were asked for, never interleaved.
    private Task WriteAsync(string text, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        lock (writeOrder)
        {
            lastWrite = WriteAfterAsync(lastWrite, bytes, cancellationToken);
            return lastWrite;
        }
    }

    private async Task WriteAfterAsync(Task previous, byte[] bytes, CancellationToken cancellationToken)
    {
        await previous.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await connection.WriteAsync(bytes, cancellationToken);
    }
}
