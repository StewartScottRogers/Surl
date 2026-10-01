using System.Net;
using System.Text;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smtp;

/// <summary>
/// What the SMTP tests share: a mail store, a server over it, an exchange context on a clock
/// the test controls, and readers for what the store holds afterwards.
/// </summary>
internal static class SmtpTestExchange
{
    public const string Greeting = "220 surl ESMTP ready\r\n";

    public const string EhloReply =
        "250-surl Hello\r\n250-SIZE 104857600\r\n250-8BITMIME\r\n250-SMTPUTF8\r\n250-PIPELINING\r\n250 ENHANCEDSTATUSCODES\r\n";

    /// <summary>
    /// When every test's clock starts: Tuesday, 29 September 2026, 08:00 UTC.
    /// </summary>
    public const string ReceivedDate = "Tue, 29 Sep 2026 08:00:00 +0000";

    public static MailboxStore AnonymousStore(TimeProvider clock, long maxMessageBytes = 0, int maxMessages = MailboxStore.DefaultMaxMessages) =>
        new([], allowAnonymous: true, clock, maxMessageBytes, maxMessages);

    public static MailboxStore AccountStore(TimeProvider clock, params string[] accountNames) =>
        new(accountNames, allowAnonymous: false, clock);

    /// <summary>
    /// The capabilities of a plaintext connection whose server can upgrade.
    /// </summary>
    public const string EhloReplyWithStartTls =
        "250-surl Hello\r\n250-SIZE 104857600\r\n250-8BITMIME\r\n250-SMTPUTF8\r\n250-PIPELINING\r\n250-ENHANCEDSTATUSCODES\r\n250 STARTTLS\r\n";

    /// <summary>
    /// A server under <c>--allow-anonymous</c> whose mail policy offers no SASL mechanism, so
    /// <c>EHLO</c> has no <c>AUTH</c> line.
    /// </summary>
    public static SmtpProtocolServer Server(MailboxStore store) => new(new AnonymousAuthenticationPolicy(), NoSaslMechanisms(), store);

    /// <summary>
    /// A server with a certificate, so <c>STARTTLS</c> upgrades.
    /// </summary>
    public static SmtpProtocolServer StartTlsServer(MailboxStore store) => new(new AnonymousAuthenticationPolicy(), NoSaslMechanisms(), store, isTlsUpgradeAvailable: true);

    public static ScriptedMailAuthenticationPolicy NoSaslMechanisms() => new([]);

    public static ExchangeContext Context(
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null,
        CancellationToken shutdownToken = default) => new(
            1,
            new ListenUrl("smtp", "127.0.0.1", 18025).WithBoundPort(18025),
            new IPEndPoint(IPAddress.Loopback, 18025),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider,
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
            ShutdownToken = shutdownToken,
        };

    /// <summary>
    /// Serves <paramref name="request"/>, sent in one read, to its end against <paramref name="store"/>.
    /// </summary>
    public static async Task<InMemoryConnection> ServeAsync(
        MailboxStore store,
        string request,
        TimeProvider clock,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null)
    {
        var connection = new InMemoryConnection(Ascii(request));
        await Server(store).ServeAsync(connection, Context(clock, cancellationToken, limits, log));
        return connection;
    }

    /// <summary>
    /// The replies written after the greeting and, when the request starts with <c>EHLO c</c>,
    /// its capabilities.
    /// </summary>
    public static string RepliesAfterHello(InMemoryConnection connection)
    {
        var written = Utf8(connection.WrittenBytes);
        Assert.StartsWith(Greeting, written);
        written = written[Greeting.Length..];
        return written.StartsWith(EhloReply, StringComparison.Ordinal) ? written[EhloReply.Length..] : written;
    }

    /// <summary>
    /// The bytes of every message in <paramref name="owner"/>'s <c>INBOX</c>, oldest first; the
    /// empty name is the anonymous owner.
    /// </summary>
    public static IReadOnlyList<string> Inbox(MailboxStore store, string owner)
    {
        var view = store.ViewFor(owner);
        Assert.AreEqual(MailStoreOutcome.Succeeded, store.ReadMailbox(view, "INBOX", out var snapshot));
        return snapshot!.Messages
            .Select(message =>
            {
                Assert.AreEqual(MailStoreOutcome.Succeeded, store.FetchMessage(view, "INBOX", message.Uid, out var bytes));
                return Encoding.UTF8.GetString(bytes.Span);
            })
            .ToList();
    }

    public static string TraceFields(string reversePath, string heloDomain = "c", string protocol = "ESMTP") =>
        $"Return-Path: <{reversePath}>\r\nReceived: from {heloDomain} ([127.0.0.1]) by surl with {protocol}; {ReceivedDate}\r\n";

    public static IEnumerable<ReadOnlyMemory<byte>> Ascii(params string[] chunks) =>
        chunks.Select(chunk => new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(chunk))).ToList();

    public static string Utf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
