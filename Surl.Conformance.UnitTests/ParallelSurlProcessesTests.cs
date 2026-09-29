using System.Diagnostics;
using System.Globalization;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

/// <summary>
/// Separate <c>surl</c> processes, each started from the <c>surl.dll</c> in the test output
/// folder, serve TFTP on loopback to the pinned upstream curl build: processes with different
/// data directories, and in-memory processes, never see each other's files, and a second
/// process given a data directory a running surl holds is refused with
/// <see cref="SurlExitCode.DataDirectoryInUse"/> while the first keeps serving (ADR-0031,
/// decisions 7 and 8). <c>SurlOnLoopback</c> runs surl in-process, which cannot prove process
/// isolation, so these tests do not use it. Inconclusive where no pinned build is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class ParallelSurlProcessesTests
{
    private const string TftpRoot = "tftp://127.0.0.1:0/";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task TwoProcesses_DifferentDirectories_EachServesOnlyItsOwnUploads()
    {
        await using var scratch = new ScratchDirectories();
        var directoryA = scratch.Create();
        var directoryB = scratch.Create();
        var uploadA = await WriteUploadAsync(scratch, "bytes from A"u8.ToArray());
        var uploadB = await WriteUploadAsync(scratch, "B's bytes, not A's"u8.ToArray());
        await using var a = await SurlProcess.StartAsync(scratch.Create(), ["--allow-uploads", "--directory", directoryA, TftpRoot], TestContext.CancellationToken);
        await using var b = await SurlProcess.StartAsync(scratch.Create(), ["--allow-uploads", "--directory", directoryB, TftpRoot], TestContext.CancellationToken);

        await AssertUploadSucceedsAsync(uploadA.Path, a.UrlOf("x"));
        await AssertUploadSucceedsAsync(uploadB.Path, b.UrlOf("x"));
        var fetchA = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", a.UrlOf("x"));
        var fetchB = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", b.UrlOf("x"));

        Assert.AreEqual(0, fetchA.ExitCode, fetchA.StandardError);
        CollectionAssert.AreEqual(uploadA.Contents, fetchA.StandardOutput);
        Assert.AreEqual(0, fetchB.ExitCode, fetchB.StandardError);
        CollectionAssert.AreEqual(uploadB.Contents, fetchB.StandardOutput);
        CollectionAssert.AreEquivalent(new[] { ".surl", "x" }, EntryNames(directoryA));
        CollectionAssert.AreEquivalent(new[] { ".surl", "x" }, EntryNames(directoryB));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task TwoProcesses_InMemory_ShareNothing()
    {
        await using var scratch = new ScratchDirectories();
        var notFoundExitCode = await ReadMissingFileExitCodeAsync();
        var upload = await WriteUploadAsync(scratch, "only A holds this"u8.ToArray());
        var workingDirectoryA = scratch.Create();
        var workingDirectoryB = scratch.Create();
        await using var a = await SurlProcess.StartAsync(workingDirectoryA, ["--allow-uploads", TftpRoot], TestContext.CancellationToken);
        await using var b = await SurlProcess.StartAsync(workingDirectoryB, ["--allow-uploads", TftpRoot], TestContext.CancellationToken);

        await AssertUploadSucceedsAsync(upload.Path, a.UrlOf("x"));
        var fetchB = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", b.UrlOf("x"));

        Assert.AreEqual(notFoundExitCode, fetchB.ExitCode, fetchB.StandardError);
        Assert.IsEmpty(fetchB.StandardOutput);
        Assert.IsEmpty(EntryNames(workingDirectoryA));
        Assert.IsEmpty(EntryNames(workingDirectoryB));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task SecondProcess_SameDirectory_IsRefusedAndFirstKeepsServing()
    {
        await using var scratch = new ScratchDirectories();
        var directory = scratch.Create();
        var upload = await WriteUploadAsync(scratch, "A still serves this"u8.ToArray());
        await using var a = await SurlProcess.StartAsync(scratch.Create(), ["--allow-uploads", "--directory", directory, TftpRoot], TestContext.CancellationToken);
        await AssertUploadSucceedsAsync(upload.Path, a.UrlOf("x"));

        var refused = await SurlProcess.RunToExitAsync(
            scratch.Create(), ["--allow-uploads", "--directory", directory, TftpRoot], StartTimeout, TestContext.CancellationToken);
        var fetchA = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", a.UrlOf("x"));

        Assert.AreEqual((int)SurlExitCode.DataDirectoryInUse, refused.ExitCode, refused.StandardError);
        Assert.AreEqual($"surl: (124) Directory {directory} is in use by another surl process", refused.StandardError.Trim());
        Assert.DoesNotContain("Listening on", refused.StandardOutput);
        Assert.AreEqual(0, fetchA.ExitCode, fetchA.StandardError);
        CollectionAssert.AreEqual(upload.Contents, fetchA.StandardOutput);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task Restart_SameDirectory_ServesTheEarlierUpload()
    {
        await using var scratch = new ScratchDirectories();
        var directory = scratch.Create();
        var upload = await WriteUploadAsync(scratch, "survives the restart"u8.ToArray());
        await using (var a = await SurlProcess.StartAsync(scratch.Create(), ["--allow-uploads", "--directory", directory, TftpRoot], TestContext.CancellationToken))
        {
            await AssertUploadSucceedsAsync(upload.Path, a.UrlOf("x"));
        }

        Assert.IsTrue(File.Exists(Path.Combine(directory, ".surl", "lock")), "A left no lock file, so the restart proves nothing about it.");
        await using var c = await SurlProcess.StartAsync(scratch.Create(), ["--directory", directory, TftpRoot], TestContext.CancellationToken);
        var fetchC = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", c.UrlOf("x"));

        Assert.AreEqual(0, fetchC.ExitCode, fetchC.StandardError);
        CollectionAssert.AreEqual(upload.Contents, fetchC.StandardOutput);
    }

    private async Task AssertUploadSucceedsAsync(string localFile, string url)
    {
        var upload = await PinnedUpstreamCurl.RunAsync(TestContext, "-sS", "-T", localFile, url);
        Assert.AreEqual(0, upload.ExitCode, upload.StandardError);
    }

    private async Task<(string Path, byte[] Contents)> WriteUploadAsync(ScratchDirectories scratch, byte[] contents)
    {
        var path = Path.Combine(scratch.Create(), "upload.bin");
        await File.WriteAllBytesAsync(path, contents, TestContext.CancellationToken);
        return (path, contents);
    }

    // The exit code the pinned build returned when the TFTP recorder answered a read of a
    // missing file, as recorded in Surl.Protocol.Tftp.UnitTests/Fixtures/missing-file.
    private async Task<int> ReadMissingFileExitCodeAsync()
    {
        var exitCodeFile = Path.Combine(
            PinnedUpstreamCurl.RepositoryRoot(), "Surl.Protocol.Tftp.UnitTests", "Fixtures", "missing-file", "exitcode.txt");
        return int.Parse(await File.ReadAllTextAsync(exitCodeFile, TestContext.CancellationToken), CultureInfo.InvariantCulture);
    }

    private static string[] EntryNames(string directory) =>
        [.. Directory.EnumerateFileSystemEntries(directory).Select(entry => Path.GetFileName(entry))];

    // Test-owned temporary directories, deleted when the test ends however it ends.
    private sealed class ScratchDirectories : IAsyncDisposable
    {
        private readonly List<DirectoryInfo> created = [];

        public string Create()
        {
            var directory = Directory.CreateTempSubdirectory("surl-parallel-");
            created.Add(directory);
            return directory.FullName;
        }

        public ValueTask DisposeAsync()
        {
            foreach (var directory in created)
            {
                directory.Delete(recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }

    // One surl process run by the dotnet host from the test output folder, killed with its
    // process tree on dispose.
    private sealed class SurlProcess : IAsyncDisposable
    {
        private const string StatusLinePrefix = "Listening on ";

        private readonly Process process;
        private readonly StringBuilder standardOutput = new();
        private readonly StringBuilder standardError = new();
        private readonly TaskCompletionSource<Uri> listening = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Uri? baseUrl;

        private SurlProcess(string workingDirectory, IReadOnlyList<string> arguments)
        {
            var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("exec");
            startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "surl.dll"));
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (_, line) => OnStandardOutputLine(line.Data);
            process.ErrorDataReceived += (_, line) => OnStandardErrorLine(line.Data);
        }

        public string UrlOf(string fileName) =>
            new Uri(baseUrl ?? throw new InvalidOperationException("surl is not listening."), fileName).AbsoluteUri;

        public static async Task<SurlProcess> StartAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            var surl = new SurlProcess(workingDirectory, arguments);
            try
            {
                surl.Begin();
                var exited = surl.process.WaitForExitAsync(cancellationToken);
                var first = await Task.WhenAny(surl.listening.Task, exited).WaitAsync(StartTimeout, cancellationToken);
                Assert.AreSame(surl.listening.Task, first, $"surl exited before listening; stderr: {surl.StandardError}");
                surl.baseUrl = await surl.listening.Task;
                return surl;
            }
            catch
            {
                await surl.DisposeAsync();
                throw;
            }
        }

        public static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunToExitAsync(
            string workingDirectory, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
        {
            await using var surl = new SurlProcess(workingDirectory, arguments);
            surl.Begin();
            await surl.process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken);

            // The parameterless wait returns once the redirected streams have been read to their end.
            surl.process.WaitForExit();
            return (surl.process.ExitCode, surl.StandardOutput, surl.StandardError);
        }

        private string StandardOutput
        {
            get
            {
                lock (standardOutput)
                {
                    return standardOutput.ToString();
                }
            }
        }

        private string StandardError
        {
            get
            {
                lock (standardError)
                {
                    return standardError.ToString();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // Never started: nothing to stop.
            }
            finally
            {
                process.Dispose();
            }
        }

        private void Begin()
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        private void OnStandardOutputLine(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (standardOutput)
            {
                standardOutput.AppendLine(line);
            }

            if (line.StartsWith(StatusLinePrefix, StringComparison.Ordinal))
            {
                listening.TrySetResult(new Uri(line[StatusLinePrefix.Length..].Trim(), UriKind.Absolute));
            }
        }

        private void OnStandardErrorLine(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (standardError)
            {
                standardError.AppendLine(line);
            }
        }
    }
}
