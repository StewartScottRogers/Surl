using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// What the FTP tests share: an FTP server over an in-memory content store holding
/// <c>/a.txt</c> (<c>hello world\n</c>, last written <see cref="FileWriteTime"/>), <c>/dir/</c>,
/// <c>/dir/sub/</c>, the dot-file <c>/.secret.txt</c>, the dot-directory <c>/.hidden/</c> with
/// <c>b.txt</c> in it and the service-state folder <c>/.surl/</c> with <c>state.txt</c> in it,
/// and an exchange context on a clock the test controls.
/// </summary>
internal static class FtpTestExchange
{
    public const string Greeting = "220 surl FTP server ready\r\n";
    public const string HeadTimedOutReply = "421 Timeout waiting for a command\r\n";
    public const string LineTooLongReply = "500 Command line too long\r\n";
    public const string ExchangeCancelledReply = "421 Timeout, closing\r\n";
    public const string LoggedIn = "230 Logged in\r\n";

    /// <summary>The bytes of <c>/a.txt</c>.</summary>
    public const string FileText = "hello world\n";

    /// <summary>The lines curl sends for its default anonymous login.</summary>
    public const string AnonymousLogin = "USER anonymous\r\nPASS ftp@example.com\r\n";

    /// <summary>The replies to <see cref="AnonymousLogin"/> when the policy lets everyone in.</summary>
    public const string AnonymousLoginReplies = "331 Password required\r\n" + LoggedIn;

    /// <summary>When every file of the standard content store was last written: the time the recorder's MDTM names.</summary>
    public static readonly DateTimeOffset FileWriteTime = new(2026, 9, 27, 12, 34, 56, TimeSpan.Zero);

    public static InMemoryContentFileSystem StandardFileSystem()
    {
        var fileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(FileWriteTime));
        var root = InMemoryContentFileSystem.RootPath;
        fileSystem.CreateDirectory(Path.Join(root, "dir"));
        fileSystem.CreateDirectory(Path.Join(root, "dir", "sub"));
        fileSystem.CreateDirectory(Path.Join(root, ".hidden"));
        fileSystem.CreateDirectory(Path.Join(root, ".surl"));
        WriteFile(fileSystem, Path.Join(root, "a.txt"), FileText);
        WriteFile(fileSystem, Path.Join(root, ".secret.txt"), "secret\n");
        WriteFile(fileSystem, Path.Join(root, ".hidden", "b.txt"), "hidden\n");
        WriteFile(fileSystem, Path.Join(root, ".surl", "state.txt"), "state\n");

        return fileSystem;
    }

    public static ContentStore StandardContentStore() =>
        new(InMemoryContentFileSystem.RootPath, StandardFileSystem(), new ContentExposureOptions());

    public static FtpProtocolServer Server(
        IAuthenticationPolicy? authenticationPolicy = null, ContentStore? contentStore = null, bool isAuthTlsAvailable = false) =>
        new(contentStore ?? StandardContentStore(), authenticationPolicy ?? new AnonymousAuthenticationPolicy(), isAuthTlsAvailable);

    public static ExchangeContext Context(
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        ExchangeLimits? limits = null,
        IExchangeLog? log = null,
        IDataConnectionOpener? dataConnections = null,
        string scheme = "ftp") => new(
            1,
            new ListenUrl(scheme, "127.0.0.1", 2121).WithBoundPort(2121),
            new IPEndPoint(IPAddress.Loopback, 2121),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            timeProvider,
            cancellationToken)
        {
            Limits = limits ?? ExchangeLimits.Default,
            DataConnections = dataConnections ?? RefusingDataConnectionOpener.Instance,
        };

    public static IEnumerable<ReadOnlyMemory<byte>> Ascii(params string[] chunks) =>
        chunks.Select(chunk => new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(chunk))).ToList();

    public static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>
    /// Serves <paramref name="request"/> to its end and returns everything the server wrote.
    /// </summary>
    public static async Task<string> ServeAsync(
        string request,
        CancellationToken cancellationToken,
        IAuthenticationPolicy? authenticationPolicy = null,
        IExchangeLog? log = null,
        IDataConnectionOpener? dataConnections = null,
        ContentStore? contentStore = null)
    {
        var connection = new InMemoryConnection(Ascii(request));
        var context = Context(new ManualTimeProvider(), cancellationToken, log: log, dataConnections: dataConnections);
        await Server(authenticationPolicy, contentStore).ServeAsync(connection, context);

        return Text(connection.WrittenBytes);
    }

    /// <summary>
    /// Serves the anonymous login and then <paramref name="commands"/>, and returns what the
    /// server wrote after the login's replies.
    /// </summary>
    public static async Task<string> ServeLoggedInAsync(
        string commands,
        CancellationToken cancellationToken,
        IDataConnectionOpener? dataConnections = null,
        IExchangeLog? log = null,
        ContentStore? contentStore = null)
    {
        var written = await ServeAsync(AnonymousLogin + commands, cancellationToken, log: log, dataConnections: dataConnections, contentStore: contentStore);
        Assert.StartsWith(Greeting + AnonymousLoginReplies, written);

        return written[(Greeting + AnonymousLoginReplies).Length..];
    }

    private static void WriteFile(InMemoryContentFileSystem fileSystem, string path, string text)
    {
        using var file = fileSystem.CreateFileForAsyncWrite(path);
        file.Write(Encoding.ASCII.GetBytes(text));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
