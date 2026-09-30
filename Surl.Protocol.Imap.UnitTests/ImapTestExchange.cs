using System.Net;
using System.Text;
using Surl.MailStore;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// What the IMAP tests share: a mail store, a server over it, an exchange context on a clock the
/// test controls, and the bytes the server wrote.
/// </summary>
internal static class ImapTestExchange
{
    /// <summary>
    /// The capabilities before a login, with the default <c>--max-filesize</c> and the clear
    /// password login offered.
    /// </summary>
    public const string Capabilities = "IMAP4rev1 SASL-IR UIDPLUS UNSELECT NAMESPACE CHILDREN ID MOVE APPENDLIMIT=104857600";

    public const string Greeting = "* OK [CAPABILITY " + Capabilities + "] surl ready\r\n";

    /// <summary>
    /// <c>UIDVALIDITY</c> of a mailbox made when every test's clock starts: Tuesday, 29 September
    /// 2026, 08:00 UTC, in seconds since 1970.
    /// </summary>
    public const uint UidValidity = 1790668800;

    public static MailboxStore AnonymousStore(TimeProvider clock) => new([], allowAnonymous: true, clock);

    public static MailboxStore AccountStore(TimeProvider clock, params string[] accountNames) => new(accountNames, allowAnonymous: false, clock);

    /// <summary>
    /// Delivers <paramref name="count"/> messages to the <c>INBOX</c> of <paramref name="owner"/>
    /// (the empty name is the anonymous owner), each <c>Subject: n</c>, an empty line and <c>hello</c>.
    /// </summary>
    public static void Deliver(MailboxStore store, string owner, int count)
    {
        var view = store.ViewFor(owner);
        for (var number = 1; number <= count; number++)
        {
            Assert.AreEqual(MailStoreOutcome.Succeeded, store.Append(view, "INBOX", Encoding.ASCII.GetBytes($"Subject: {number}\r\n\r\nhello\r\n"), MailFlags.None, null, out _));
        }
    }

    public static void Create(MailboxStore store, string owner, params string[] mailboxNames)
    {
        foreach (var name in mailboxNames)
        {
            Assert.AreEqual(MailStoreOutcome.Succeeded, store.CreateMailbox(store.ViewFor(owner), name));
        }
    }

    /// <summary>
    /// A server whose policy answers every login <see cref="PasswordLoginVerdict.AcceptedUnchecked"/>,
    /// as <c>--allow-anonymous</c> does, and offers the clear-password login and no SASL mechanism,
    /// as the recordings in <c>Fixtures/</c> were served.
    /// </summary>
    public static ImapProtocolServer Server(MailboxStore store) =>
        Server(store, new ScriptedLoginPolicy(PasswordLoginVerdict.AcceptedUnchecked, PasswordLoginVerdict.AcceptedUnchecked));

    public static ImapProtocolServer Server(MailboxStore store, ScriptedLoginPolicy policy, bool isStartTlsAvailable = false) =>
        new(policy, policy, store, isStartTlsAvailable);

    public static ExchangeContext Context(
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null) => new(
            1,
            new ListenUrl("imap", "127.0.0.1", 18143).WithBoundPort(18143),
            new IPEndPoint(IPAddress.Loopback, 18143),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider,
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };

    /// <summary>
    /// Serves <paramref name="request"/>, sent in one read, to its end.
    /// </summary>
    public static async Task<InMemoryConnection> ServeAsync(
        ImapProtocolServer server,
        string request,
        TimeProvider clock,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null)
    {
        var connection = new InMemoryConnection(Bytes(request));
        await server.ServeAsync(connection, Context(clock, cancellationToken, limits, log));
        return connection;
    }

    /// <summary>
    /// Serves <paramref name="request"/> against an anonymous store holding what
    /// <paramref name="fill"/> puts in it, and returns what was written after the greeting.
    /// </summary>
    public static async Task<string> ResponsesAsync(string request, CancellationToken cancellationToken, Action<MailboxStore>? fill = null, ExchangeLimits? limits = null, IExchangeLog? log = null)
    {
        var clock = new ManualTimeProvider();
        var store = AnonymousStore(clock);
        fill?.Invoke(store);
        var connection = await ServeAsync(Server(store), request, clock, cancellationToken, limits, log);
        return AfterGreeting(connection);
    }

    public static string AfterGreeting(InMemoryConnection connection) => AfterGreeting(connection.WrittenBytes);

    public static string AfterGreeting(byte[] writtenBytes)
    {
        var written = Utf8(writtenBytes);
        Assert.StartsWith(Greeting, written);
        return written[Greeting.Length..];
    }

    public static IEnumerable<ReadOnlyMemory<byte>> Bytes(params string[] chunks) =>
        chunks.Select(chunk => new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(chunk))).ToList();

    public static string Utf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
