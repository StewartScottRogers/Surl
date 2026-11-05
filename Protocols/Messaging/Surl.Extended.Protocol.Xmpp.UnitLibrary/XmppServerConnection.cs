using System.Text;
using System.Xml.Linq;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// One XMPP connection (ADR-0114 decisions 1, 6 and 7): the client's stream header within the head
/// timeout, the response header and features, then first-level elements until the client closes
/// the stream, the input ends, or a stream error ends it.
/// </summary>
internal sealed class XmppServerConnection
{
    /// <summary>How long the server tries to write its farewell once the exchange was cancelled for a limit or shutdown.</summary>
    public static readonly TimeSpan FarewellWriteDeadline = TimeSpan.FromSeconds(1);

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly XmppServerOptions options;
    private readonly XmppStreamReader reader;
    private readonly IReadOnlyList<string> domains;
    private bool headerSent;

    public XmppServerConnection(IConnection connection, ExchangeContext context, XmppServerOptions options)
    {
        this.connection = connection;
        this.context = context;
        this.options = options;
        reader = new XmppStreamReader(connection, context.Limits.MaxMessageBytes);
        domains = XmppDomains.Served(context.ListenUrl, context.LocalEndPoint, options.MailDomains);
    }

    public async Task ServeAsync()
    {
        try
        {
            await ServeStreamAsync();
            await connection.CompleteWritesAsync(context.CancellationToken);
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

    private async Task ServeStreamAsync()
    {
        if (await ReadHeaderWithinHeadTimeoutAsync() is not { } header)
        {
            return;
        }

        await OpenStreamAsync(header);
        if (header.IsEmptyElement)
        {
            await WriteAsync(XmppStreamText.Close);
            return;
        }

        // Every first-level element ends the stream until BL-537 to BL-539 serve one, so one is
        // read; serving them makes this a loop.
        if (await reader.ReadElementAsync(context.CancellationToken) is { } element)
        {
            throw Refusal(element);
        }

        if (reader.ClientClosedStream)
        {
            context.Log.Note("The client closed the stream; closed.");
            await WriteAsync(XmppStreamText.Close);
        }
    }

    // Decision 7: the head timeout runs from accept to a complete stream header.
    private async Task<XmppStreamHeader?> ReadHeaderWithinHeadTimeoutAsync()
    {
        using var headTimeout = new CancellationTokenSource(context.Limits.HeadTimeout, context.TimeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, headTimeout.Token);
        try
        {
            return await reader.ReadHeaderAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            throw new XmppStreamErrorException(XmppStreamErrors.ConnectionTimeout, "No stream header arrived within the head timeout");
        }
    }

    // Decision 1: the domain, then the version, then the response header and features.
    private async Task OpenStreamAsync(XmppStreamHeader header)
    {
        var domain = StreamDomain(header.To);
        RequireVersion1(header.Version);
        var language = string.IsNullOrEmpty(header.Language) ? XmppStreamText.DefaultLanguage : header.Language;
        await WriteHeaderAsync(domain, language);
        await WriteAsync(XmppStreamText.NoFeatures);
        context.Log.Note($"Opened a stream for {domain}.");
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

    // Until STARTTLS (BL-537), SASL (BL-538) and stanzas (BL-539) are served, no first-level
    // element is: a stanza is not authorized, any other element in jabber:client is not a stanza
    // type, and any other element is not allowed in this phase.
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
