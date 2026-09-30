using System.Net;
using System.Text;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Pop3;

/// <summary>
/// What the POP3 tests share: a mail store holding the recorder's message, a server over it, an
/// exchange context on a clock the test controls, and readers for what the store holds afterwards.
/// </summary>
internal static class Pop3TestExchange
{
    public const string Greeting = "+OK surl ready\r\n";

    /// <summary>
    /// The message <c>Record-CurlExchange.ps1 -Pop3</c> serves by default: 133 bytes, its last
    /// line starting with a dot.
    /// </summary>
    public const string Message =
        "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n.A line that starts with a dot.\r\n";

    /// <summary>
    /// <see cref="Message"/> dot-stuffed and ended, as <c>RETR</c> sends it.
    /// </summary>
    public const string StuffedMessage =
        "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n..A line that starts with a dot.\r\n.\r\n";

    /// <summary>
    /// The <c>UIDVALIDITY</c> every test store's <c>INBOX</c> gets: the test clock's start,
    /// 2026-09-29T08:00:00Z, in Unix seconds.
    /// </summary>
    public const string UidValidity = "1790668800";

    /// <summary>
    /// <c>USER u</c> and <c>PASS p</c>, which <see cref="Pop3TestPolicy"/> accepts.
    /// </summary>
    public const string Login = "USER u\r\nPASS p\r\n";

    /// <summary>
    /// The replies to <see cref="Login"/>.
    /// </summary>
    public const string LoginReplies = "+OK User accepted\r\n+OK Logged in\r\n";

    /// <summary>
    /// A store with the account <c>u</c>, whose <c>INBOX</c> holds <paramref name="messages"/>.
    /// </summary>
    public static MailboxStore AccountStore(TimeProvider clock, params string[] messages) =>
        Seed(new MailboxStore(["u"], allowAnonymous: false, clock), "u@x", messages);

    /// <summary>
    /// A store under <c>--allow-anonymous</c>, whose anonymous <c>INBOX</c> holds <paramref name="messages"/>.
    /// </summary>
    public static MailboxStore AnonymousStore(TimeProvider clock, params string[] messages) =>
        Seed(new MailboxStore([], allowAnonymous: true, clock), "anyone@x", messages);

    public static MailboxStore Seed(MailboxStore store, string recipientPath, IEnumerable<string> messages)
    {
        Assert.AreEqual(MailRecipientLookup.Deliverable, store.LookUpRecipient(recipientPath, out var recipient));
        foreach (var message in messages)
        {
            Assert.AreEqual(MailStoreOutcome.Succeeded, store.Deliver([recipient!], Encoding.ASCII.GetBytes(message)));
        }

        return store;
    }

    public static Pop3ProtocolServer Server(MailboxStore store, Pop3TestPolicy? policy = null, bool isStlsAvailable = false)
    {
        policy ??= new Pop3TestPolicy();
        return new Pop3ProtocolServer(policy, policy, store, isStlsAvailable, new PatternRandomNumberGenerator());
    }

    public static ExchangeContext Context(
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null) => new(
            1,
            new ListenUrl("pop3", "127.0.0.1", 18110).WithBoundPort(18110),
            new IPEndPoint(IPAddress.Loopback, 18110),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider,
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };

    /// <summary>
    /// Serves <paramref name="request"/>, sent in one read, to its end against <paramref name="store"/>.
    /// </summary>
    public static async Task<InMemoryConnection> ServeAsync(
        MailboxStore store,
        string request,
        TimeProvider clock,
        CancellationToken cancellationToken,
        Pop3TestPolicy? policy = null,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null,
        bool isStlsAvailable = false)
    {
        var connection = new InMemoryConnection(Ascii(request));
        await Server(store, policy, isStlsAvailable).ServeAsync(connection, Context(clock, cancellationToken, limits, log));
        return connection;
    }

    /// <summary>
    /// The replies written after the greeting.
    /// </summary>
    public static string RepliesAfterGreeting(InMemoryConnection connection)
    {
        var written = Utf8(connection.WrittenBytes);
        Assert.StartsWith(Greeting, written);
        return written[Greeting.Length..];
    }

    /// <summary>
    /// The replies written after the greeting and <see cref="LoginReplies"/>.
    /// </summary>
    public static string RepliesAfterLogin(InMemoryConnection connection)
    {
        var written = RepliesAfterGreeting(connection);
        Assert.StartsWith(LoginReplies, written);
        return written[LoginReplies.Length..];
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

    public static IEnumerable<ReadOnlyMemory<byte>> Ascii(params string[] chunks) =>
        chunks.Select(chunk => new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(chunk))).ToList();

    public static string Utf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
