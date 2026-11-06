using System.Globalization;
using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// What the SASL and binding tests share (BL-538): the client's stream header and the server's
/// answers written by hand from RFC 6120 sections 6.4, 7 and 9.1, and a run of the server over an
/// in-memory connection that is TLS from its first byte unless asked otherwise, so the login
/// phase starts at once. Stream IDs and other random values come from <see cref="CountingRandomSource"/>.
/// </summary>
internal static class XmppLoginScript
{
    public const string Streams = "xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams'";

    // RFC 6120 section 9.1.1, steps 1 and 7, on one line.
    public const string ClientHeader =
        "<?xml version='1.0'?><stream:stream to='im.example.com' version='1.0' xml:lang='en' " + Streams + ">";

    public const string Close = "</stream:stream>";

    public const string BindFeatures = "<stream:features><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'/></stream:features>";

    public const string Success = "<success xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>";

    public static string ResponseHeader(int draw) =>
        $"<?xml version='1.0'?><stream:stream from='im.example.com' id='{Hex(draw, 32)}' version='1.0' xml:lang='en' " + Streams + ">";

    public static string Hex(int draw, int digits) => draw.ToString("x", CultureInfo.InvariantCulture).PadLeft(digits, '0');

    public static string MechanismFeatures(params string[] mechanisms) =>
        "<stream:features><mechanisms xmlns='urn:ietf:params:xml:ns:xmpp-sasl'>" +
        string.Concat(mechanisms.Select(name => $"<mechanism>{name}</mechanism>")) + "</mechanisms></stream:features>";

    public static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    public static string Auth(string mechanism, string text) =>
        $"<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='{mechanism}'>{text}</auth>";

    public static string Response(string text) => $"<response xmlns='urn:ietf:params:xml:ns:xmpp-sasl'>{text}</response>";

    public static string Challenge(string text) => $"<challenge xmlns='urn:ietf:params:xml:ns:xmpp-sasl'>{text}</challenge>";

    public static string Failure(string condition) => $"<failure xmlns='urn:ietf:params:xml:ns:xmpp-sasl'><{condition}/></failure>";

    public static string ErrorAndClose(string condition) =>
        $"<stream:error><{condition} xmlns='urn:ietf:params:xml:ns:xmpp-streams'/></stream:error></stream:stream>";

    // RFC 4616's PLAIN message for user/pencil.
    public static string PlainUserPencil => Base64("\0user\0pencil");

    public static UnitTestXmppSaslPolicy Policy() => new([], ["SCRAM-SHA-256", "SCRAM-SHA-1", "PLAIN"]);

    // Accepts user/pencil over PLAIN as the account user, and refuses anything else.
    public static UnitTestXmppSaslPolicy PlainPolicy(string accountName = "user") => new([], ["PLAIN"])
    {
        Answer = message => message is not null && Encoding.UTF8.GetString(message) == "\0user\0pencil"
            ? new SaslLoginStep(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, accountName, new CheckedLogin("PLAIN", "user", true))
            : new SaslLoginStep(SaslLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, new CheckedLogin("PLAIN", "user", false)),
    };

    public static XmppServerOptions Options(ISaslAuthenticationPolicy policy) => new()
    {
        RandomSource = new CountingRandomSource(),
        IsTlsUpgradeAvailable = true,
        AuthenticationPolicy = policy,
    };

    public static async Task<(string Answers, RecordingExchangeLog Log)> ServeAsync(
        XmppServerOptions options, CancellationToken cancellationToken, bool isTls, params string[] chunks)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(
            chunks.Select(chunk => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(chunk)),
            new IPEndPoint(IPAddress.Loopback, 5223),
            initialTlsSession: isTls ? InMemoryConnection.DefaultUpgradeTlsSession : null);
        var context = new ExchangeContext(
            1,
            new ListenUrl(isTls ? "xmpps" : "xmpp", "im.example.com", 5223).WithBoundPort(5223),
            new IPEndPoint(IPAddress.Loopback, 5223),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log,
            new ManualTimeProvider(),
            cancellationToken);

        await new XmppServer(options).ServeAsync(connection, context);

        return (Encoding.UTF8.GetString(connection.WrittenBytes), log);
    }

    public static Task<(string Answers, RecordingExchangeLog Log)> ServeAsync(
        XmppServerOptions options, CancellationToken cancellationToken, params string[] chunks) =>
        ServeAsync(options, cancellationToken, isTls: true, chunks);
}
