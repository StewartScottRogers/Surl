using Surl.Console;

namespace Surl.Conformance;

/// <summary>
/// <c>surl</c> running in-process on an ephemeral loopback port, serving a fresh temporary
/// directory, a directory the caller owns, or, without <c>--directory</c>, its own in-memory
/// file system, through the same entry point the executable uses. Disposing it stops surl and
/// deletes the temporary directory, if any.
/// </summary>
internal sealed class SurlOnLoopback : IAsyncDisposable
{
    private const string StatusLinePrefix = "Listening on ";

    private readonly DirectoryInfo? servedDirectory;
    private readonly CancellationTokenSource stop;
    private readonly Task<int> running;

    private SurlOnLoopback(DirectoryInfo? servedDirectory, CancellationTokenSource stop, Task<int> running, Uri baseUrl)
    {
        this.servedDirectory = servedDirectory;
        this.stop = stop;
        this.running = running;
        BaseUrl = baseUrl;
    }

    /// <summary>
    /// Gets the URL of the served root, with the port surl bound, such as
    /// <c>http://127.0.0.1:49731/</c>.
    /// </summary>
    public Uri BaseUrl { get; }

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
    public static async Task<SurlOnLoopback> StartAsync(
        string scheme,
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
            directory, [.. options, "--directory", directory.FullName, $"{scheme}://127.0.0.1:0/"], cancellationToken);
    }

    /// <summary>
    /// Starts surl on <c><paramref name="scheme"/>://127.0.0.1:0/</c> with <paramref name="options"/>
    /// and no <c>--directory</c>, so it serves a new, empty in-memory file system, returning once
    /// surl has written its status line.
    /// </summary>
    public static Task<SurlOnLoopback> StartInMemoryAsync(
        string scheme, IReadOnlyList<string> options, CancellationToken cancellationToken) =>
        StartServingAsync(null, [.. options, $"{scheme}://127.0.0.1:0/"], cancellationToken);

    /// <summary>
    /// Starts surl on <c><paramref name="scheme"/>://127.0.0.1:0/</c> with
    /// <paramref name="options"/> and <c>--directory <paramref name="dataDirectory"/></c>, an existing directory the caller
    /// owns, returning once surl has written its status line. Disposing it stops surl and
    /// leaves the directory as surl left it, so another surl can start over it.
    /// </summary>
    public static Task<SurlOnLoopback> StartOverDirectoryAsync(
        string scheme, string dataDirectory, IReadOnlyList<string> options, CancellationToken cancellationToken) =>
        StartServingAsync(null, [.. options, "--directory", dataDirectory, $"{scheme}://127.0.0.1:0/"], cancellationToken);

    private static async Task<SurlOnLoopback> StartServingAsync(
        DirectoryInfo? directory, string[] args, CancellationToken cancellationToken)
    {
        var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var output = new FirstLineWriter();
        var running = Program.RunAsync(args, output, TextWriter.Null, stop.Token);
        var statusLine = await Task.WhenAny(output.FirstLine, running) == running
            ? throw new InvalidOperationException($"surl exited {await running} before it listened.")
            : await output.FirstLine.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        return new SurlOnLoopback(directory, stop, running, new Uri(statusLine[StatusLinePrefix.Length..]));
    }

    /// <summary>
    /// Gets the URL of <paramref name="relativePath"/> under the served root.
    /// </summary>
    public string UrlOf(string relativePath) => new Uri(BaseUrl, relativePath).AbsoluteUri;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        await running;
        stop.Dispose();
        servedDirectory?.Delete(recursive: true);
    }

    /// <summary>A thread-safe writer that hands out the first whole line written to it.</summary>
    private sealed class FirstLineWriter : StringWriter
    {
        private readonly TaskCompletionSource<string> firstLine = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Lock writeLock = new();

        public Task<string> FirstLine => firstLine.Task;

        public override void Write(char value) => Write(value.ToString());

        public override void Write(string? value)
        {
            lock (writeLock)
            {
                base.Write(value);
                var text = ToString();
                var end = text.IndexOf(Environment.NewLine, StringComparison.Ordinal);
                if (end >= 0)
                {
                    firstLine.TrySetResult(text[..end]);
                }
            }
        }
    }
}
