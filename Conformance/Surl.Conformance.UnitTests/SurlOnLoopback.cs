using Surl.Console;

namespace Surl.Conformance;

/// <summary>
/// <c>surl</c> running in-process on an ephemeral loopback port, serving a fresh temporary
/// directory, a directory the caller owns, or, without <c>--directory</c>, its own in-memory
/// file system, through the same entry point the executable uses. When <c>SURL_EXECUTABLE</c>
/// holds the path of a published <c>surl</c>, every start runs that binary as a process with the
/// same arguments instead (<see cref="PublishedSurlProcess"/>, ADR-0085 decision 2). Disposing it
/// stops surl and deletes the temporary directory, if any.
/// </summary>
internal sealed class SurlOnLoopback : IAsyncDisposable
{
    private const string StatusLinePrefix = "Listening on ";
    private const string UnixSocketSeparator = " over unix://";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    private readonly DirectoryInfo? servedDirectory;
    private readonly Func<string> readLog;
    private readonly Func<ValueTask> stopSurlAsync;

    private SurlOnLoopback(DirectoryInfo? servedDirectory, IReadOnlyList<string> statusLines, Func<string> readLog, Func<ValueTask> stopSurlAsync)
    {
        this.servedDirectory = servedDirectory;
        this.readLog = readLog;
        this.stopSurlAsync = stopSurlAsync;
        // A listener on a Unix socket says so after its URL: "Listening on ftp://localhost:21/ over unix://<path>".
        BaseUrls = [.. statusLines.Select(line => new Uri(line[StatusLinePrefix.Length..].Split(UnixSocketSeparator)[0]))];
    }

    /// <summary>
    /// Gets the URL of the served root, with the port surl bound, such as
    /// <c>http://127.0.0.1:49731/</c>: the first listener's, when surl listens on more than one.
    /// </summary>
    public Uri BaseUrl => BaseUrls[0];

    /// <summary>
    /// Gets the URL of the served root of every listener surl bound, one per status line, in
    /// the order surl wrote them.
    /// </summary>
    public IReadOnlyList<Uri> BaseUrls { get; }

    /// <summary>
    /// Gets the full path of the temporary directory surl serves, when it serves one this
    /// instance made; otherwise <see langword="null"/>.
    /// </summary>
    public string? ServedDirectory => servedDirectory?.FullName;

    /// <summary>
    /// Gets everything surl has written to its error stream so far: its warnings and, with
    /// <c>-v</c>, its log notes.
    /// </summary>
    public string Log => readLog();

    /// <summary>
    /// Writes <paramref name="files"/> into a new temporary directory and starts surl serving
    /// it on <c>http://127.0.0.1:0/</c>, returning once surl has written its status line.
    /// </summary>
    public static Task<SurlOnLoopback> StartAsync(
        IReadOnlyDictionary<string, byte[]> files, CancellationToken cancellationToken) =>
        StartAsync("http", files, [], [], cancellationToken);

    /// <summary>
    /// Creates <paramref name="subdirectories"/> and writes <paramref name="files"/> into a new
    /// temporary directory, and starts surl serving it on <c><paramref name="scheme"/>://127.0.0.1:0/</c>
    /// with <paramref name="options"/> before the listen URL,
    /// returning once surl has written its status line.
    /// </summary>
    public static Task<SurlOnLoopback> StartAsync(
        string scheme,
        IReadOnlyDictionary<string, byte[]> files,
        IReadOnlyList<string> subdirectories,
        IReadOnlyList<string> options,
        CancellationToken cancellationToken) =>
        StartOnPortAsync(scheme, 0, files, subdirectories, options, cancellationToken);

    /// <summary>
    /// As <see cref="StartAsync(string, IReadOnlyDictionary{string, byte[]}, IReadOnlyList{string}, IReadOnlyList{string}, CancellationToken)"/>,
    /// but on <c><paramref name="scheme"/>://127.0.0.1:<paramref name="port"/>/</c>, for a test
    /// that must name the port before surl starts, such as in a Kerberos service principal.
    /// </summary>
    public static async Task<SurlOnLoopback> StartOnPortAsync(
        string scheme,
        int port,
        IReadOnlyDictionary<string, byte[]> files,
        IReadOnlyList<string> subdirectories,
        IReadOnlyList<string> options,
        CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("surl-conformance-");
        foreach (var subdirectory in subdirectories)
        {
            directory.CreateSubdirectory(subdirectory);
        }

        foreach (var (name, contents) in files)
        {
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, name), contents, cancellationToken);
        }

        return await StartServingAsync(
            directory, [.. options, "--directory", directory.FullName, $"{scheme}://127.0.0.1:{port}/"], cancellationToken);
    }

    /// <summary>
    /// Writes <paramref name="files"/> into a new temporary directory and starts surl serving it
    /// on <c><paramref name="scheme"/>://localhost/</c> with the control connection on the Unix
    /// socket file <paramref name="socketPath"/> (<c>--unix-socket</c>), returning once surl has
    /// written its status line. <see cref="BaseUrl"/> names <c>localhost</c> and the scheme's
    /// default port, which nothing listens on.
    /// </summary>
    public static async Task<SurlOnLoopback> StartOverUnixSocketAsync(
        string scheme,
        string socketPath,
        IReadOnlyDictionary<string, byte[]> files,
        IReadOnlyList<string> subdirectories,
        IReadOnlyList<string> options,
        CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("surl-conformance-");
        foreach (var subdirectory in subdirectories)
        {
            directory.CreateSubdirectory(subdirectory);
        }

        foreach (var (name, contents) in files)
        {
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, name), contents, cancellationToken);
        }

        return await StartServingAsync(
            directory, [.. options, "--unix-socket", socketPath, "--directory", directory.FullName, $"{scheme}://localhost/"], cancellationToken);
    }

    /// surl has written its status line.
    /// </summary>
    public static Task<SurlOnLoopback> StartInMemoryAsync(
        string scheme, IReadOnlyList<string> options, CancellationToken cancellationToken) =>
        StartServingAsync(null, [.. options, $"{scheme}://127.0.0.1:0/"], cancellationToken);

    /// <summary>
    /// Starts one surl listening on <c><em>scheme</em>://127.0.0.1:0/</c> for each of
    /// <paramref name="schemes"/>, in order, with <paramref name="options"/> and no
    /// <c>--directory</c>, so every listener shares one new, empty in-memory store, returning
    /// once surl has written a status line for every listener; <see cref="BaseUrls"/> holds
    /// each listener's URL.
    /// </summary>
    public static Task<SurlOnLoopback> StartInMemoryAsync(
        IReadOnlyList<string> schemes, IReadOnlyList<string> options, CancellationToken cancellationToken) =>
        StartServingAsync(
            null,
            [.. options, .. schemes.Select(scheme => $"{scheme}://127.0.0.1:0/")],
            cancellationToken,
            schemes.Count);

    /// <summary>
    /// Starts surl on <c><paramref name="scheme"/>://127.0.0.1:0/</c> with
    /// <paramref name="options"/> and <c>--directory <paramref name="dataDirectory"/></c>, an existing directory the caller
    /// owns, returning once surl has written its status line. Disposing it stops surl and
    /// leaves the directory as surl left it, so another surl can start over it.
    /// </summary>
    public static Task<SurlOnLoopback> StartOverDirectoryAsync(
        string scheme, string dataDirectory, IReadOnlyList<string> options, CancellationToken cancellationToken) =>
        StartOverDirectoryAsync([scheme], dataDirectory, options, cancellationToken);

    /// <summary>
    /// Starts one surl listening on <c><em>scheme</em>://127.0.0.1:0/</c> for each of
    /// <paramref name="schemes"/>, with <paramref name="options"/> and
    /// <c>--directory <paramref name="dataDirectory"/></c>, an existing directory the caller
    /// owns, returning once surl has written a status line for every listener; find each
    /// listener's URL with <see cref="BaseUrlOf"/>. Disposing it stops surl and leaves the
    /// directory as surl left it.
    /// </summary>
    public static Task<SurlOnLoopback> StartOverDirectoryAsync(
        IReadOnlyList<string> schemes, string dataDirectory, IReadOnlyList<string> options, CancellationToken cancellationToken) =>
        StartServingAsync(
            null,
            [.. options, "--directory", dataDirectory, .. schemes.Select(scheme => $"{scheme}://127.0.0.1:0/")],
            cancellationToken,
            schemes.Count);

    private static async Task<SurlOnLoopback> StartServingAsync(
        DirectoryInfo? directory, string[] args, CancellationToken cancellationToken, int listenerCount = 1)
    {
        try
        {
            return PublishedSurlProcess.ConfiguredExecutablePath is { } executablePath
                ? await StartPublishedAsync(executablePath, directory, args, cancellationToken, listenerCount)
                : await StartInProcessAsync(directory, args, cancellationToken, listenerCount);
        }
        catch
        {
            directory?.Delete(recursive: true);
            throw;
        }
    }

    /// <summary>
    /// Starts the published surl at <paramref name="executablePath"/> as a process with
    /// <paramref name="args"/>, as every start does when <c>SURL_EXECUTABLE</c> names it
    /// (ADR-0085, decision 2).
    /// </summary>
    internal static async Task<SurlOnLoopback> StartPublishedAsync(
        string executablePath, DirectoryInfo? directory, string[] args, CancellationToken cancellationToken, int listenerCount = 1)
    {
        var (surl, statusLines) = await PublishedSurlProcess.StartAsync(
            executablePath, args, listenerCount, StartTimeout, cancellationToken);

        return new SurlOnLoopback(directory, statusLines, () => surl.Log, surl.DisposeAsync);
    }

    private static async Task<SurlOnLoopback> StartInProcessAsync(
        DirectoryInfo? directory, string[] args, CancellationToken cancellationToken, int listenerCount)
    {
        var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var output = new StatusLinesWriter(listenerCount);
        var log = new LogWriter();
        var running = Program.RunAsync(args, output, log, stop.Token);
        var statusLines = await Task.WhenAny(output.StatusLines, running) == running
            ? throw new InvalidOperationException($"surl exited {await running} before it listened.")
            : await output.StatusLines.WaitAsync(StartTimeout, cancellationToken);

        return new SurlOnLoopback(directory, statusLines, () => log.Text, async () =>
        {
            await stop.CancelAsync();
            await running;
            stop.Dispose();
        });
    }

    /// <summary>
    /// Gets the URL of the served root of the listener for <paramref name="scheme"/>.
    /// </summary>
    public Uri BaseUrlOf(string scheme) => BaseUrls.Single(url => url.Scheme == scheme);

    /// <summary>
    /// Gets the URL of <paramref name="relativePath"/> under the served root.
    /// </summary>
    public string UrlOf(string relativePath) => new Uri(BaseUrl, relativePath).AbsoluteUri;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await stopSurlAsync();
        servedDirectory?.Delete(recursive: true);
    }

    /// <summary>A thread-safe writer that keeps everything written to it.</summary>
    private sealed class LogWriter : StringWriter
    {
        private readonly Lock writeLock = new();

        public string Text
        {
            get
            {
                lock (writeLock)
                {
                    return ToString();
                }
            }
        }

        public override void Write(char value) => Write(value.ToString());

        public override void Write(char[] buffer, int index, int count) => Write(new string(buffer, index, count));

        public override void Write(string? value)
        {
            lock (writeLock)
            {
                base.Write(value);
            }
        }
    }

    /// <summary>
    /// A thread-safe writer that hands out the first <c>count</c> whole lines written to it:
    /// surl's status lines, one per listener.
    /// </summary>
    private sealed class StatusLinesWriter(int count) : StringWriter
    {
        private readonly TaskCompletionSource<string[]> statusLines = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Lock writeLock = new();

        public Task<string[]> StatusLines => statusLines.Task;

        public override void Write(char value) => Write(value.ToString());

        public override void Write(string? value)
        {
            lock (writeLock)
            {
                base.Write(value);
                var lines = ToString().Split(Environment.NewLine);

                // The last piece is the unfinished line after the last line end.
                if (lines.Length > count)
                {
                    statusLines.TrySetResult(lines[..count]);
                }
            }
        }
    }
}
