using Surl.Console;

namespace Surl.Conformance;

/// <summary>
/// <c>surl</c> running in-process on an ephemeral loopback port, serving a fresh temporary
/// directory, through the same entry point the executable uses. Disposing it stops surl and
/// deletes the directory.
/// </summary>
internal sealed class SurlOnLoopback : IAsyncDisposable
{
    private const string StatusLinePrefix = "Listening on ";

    private readonly DirectoryInfo servedDirectory;
    private readonly CancellationTokenSource stop;
    private readonly Task<int> running;

    private SurlOnLoopback(DirectoryInfo servedDirectory, CancellationTokenSource stop, Task<int> running, Uri baseUrl)
    {
        this.servedDirectory = servedDirectory;
        this.stop = stop;
        this.running = running;
        BaseUrl = baseUrl;
    }

    /// <summary>
    /// Gets the URL of the served directory's root, with the port surl bound, such as
    /// <c>http://127.0.0.1:49731/</c>.
    /// </summary>
    public Uri BaseUrl { get; }

    /// <summary>
    /// Writes <paramref name="files"/> into a new temporary directory and starts surl serving
    /// it on <c>http://127.0.0.1:0/</c>, returning once surl has written its status line.
    /// </summary>
    public static async Task<SurlOnLoopback> StartAsync(
        IReadOnlyDictionary<string, byte[]> files, CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("surl-conformance-");
        foreach (var (name, contents) in files)
        {
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, name), contents, cancellationToken);
        }

        var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var output = new FirstLineWriter();
        var running = Program.RunAsync(
            ["--directory", directory.FullName, "http://127.0.0.1:0/"], output, TextWriter.Null, stop.Token);
        var statusLine = await Task.WhenAny(output.FirstLine, running) == running
            ? throw new InvalidOperationException($"surl exited {await running} before it listened.")
            : await output.FirstLine.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        return new SurlOnLoopback(directory, stop, running, new Uri(statusLine[StatusLinePrefix.Length..]));
    }

    /// <summary>
    /// Gets the URL of <paramref name="relativePath"/> under the served directory.
    /// </summary>
    public string UrlOf(string relativePath) => new Uri(BaseUrl, relativePath).AbsoluteUri;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        await running;
        stop.Dispose();
        servedDirectory.Delete(recursive: true);
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
