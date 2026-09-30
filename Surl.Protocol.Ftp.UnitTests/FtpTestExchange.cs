using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// What the FTP tests share: an FTP server over an in-memory content store holding
/// <c>/a.txt</c>, <c>/dir/</c>, <c>/dir/sub/</c>, the dot-directory <c>/.hidden/</c> and the
/// service-state folder <c>/.surl/</c>, and an exchange context on a clock the test controls.
/// </summary>
internal static class FtpTestExchange
{
    public const string Greeting = "220 surl FTP server ready\r\n";
    public const string HeadTimedOutReply = "421 Timeout waiting for a command\r\n";
    public const string LineTooLongReply = "500 Command line too long\r\n";
    public const string LoggedIn = "230 Logged in\r\n";

    /// <summary>The lines curl sends for its default anonymous login.</summary>
    public const string AnonymousLogin = "USER anonymous\r\nPASS ftp@example.com\r\n";

    /// <summary>The replies to <see cref="AnonymousLogin"/> when the policy lets everyone in.</summary>
    public const string AnonymousLoginReplies = "331 Password required\r\n" + LoggedIn;

    public static ContentStore StandardContentStore()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        var root = InMemoryContentFileSystem.RootPath;
        fileSystem.CreateDirectory(Path.Join(root, "dir"));
        fileSystem.CreateDirectory(Path.Join(root, "dir", "sub"));
        fileSystem.CreateDirectory(Path.Join(root, ".hidden"));
        fileSystem.CreateDirectory(Path.Join(root, ".surl"));
        using (var file = fileSystem.CreateFileForAsyncWrite(Path.Join(root, "a.txt")))
        {
            file.Write("hello world\n"u8);
        }

        return new ContentStore(root, fileSystem, new ContentExposureOptions());
    }

    public static FtpProtocolServer Server(IAuthenticationPolicy? authenticationPolicy = null) =>
        new(StandardContentStore(), authenticationPolicy ?? new AnonymousAuthenticationPolicy());

    public static ExchangeContext Context(
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null) => new(
            1,
            new ListenUrl("ftp", "127.0.0.1", 2121).WithBoundPort(2121),
            new IPEndPoint(IPAddress.Loopback, 2121),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider,
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
        };

    public static IEnumerable<ReadOnlyMemory<byte>> Ascii(params string[] chunks) =>
        chunks.Select(chunk => new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(chunk))).ToList();

    public static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>
    /// Serves <paramref name="request"/> to its end and returns everything the server wrote.
    /// </summary>
    public static async Task<string> ServeAsync(
        string request, CancellationToken cancellationToken, IAuthenticationPolicy? authenticationPolicy = null, IExchangeLog? log = null)
    {
        var connection = new InMemoryConnection(Ascii(request));
        await Server(authenticationPolicy).ServeAsync(connection, Context(new ManualTimeProvider(), cancellationToken, log: log));

        return Text(connection.WrittenBytes);
    }

    /// <summary>
    /// Serves the anonymous login and then <paramref name="commands"/>, and returns what the
    /// server wrote after the login's replies.
    /// </summary>
    public static async Task<string> ServeLoggedInAsync(string commands, CancellationToken cancellationToken)
    {
        var written = await ServeAsync(AnonymousLogin + commands, cancellationToken);
        Assert.StartsWith(Greeting + AnonymousLoginReplies, written);

        return written[(Greeting + AnonymousLoginReplies).Length..];
    }
}
