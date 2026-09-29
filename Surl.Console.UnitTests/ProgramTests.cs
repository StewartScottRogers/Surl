using System.Runtime.InteropServices;
using Surl.Cli;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

[TestClass]
public sealed class ProgramTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DoNotParallelize]
    public async Task Main_Help_WritesHelpTextToStandardOutputAndReturnsOk()
    {
        var originalOutput = System.Console.Out;
        using var output = new StringWriter();
        System.Console.SetOut(output);

        int exitCode;
        try
        {
            exitCode = await Program.Main(["--help"]);
        }
        finally
        {
            System.Console.SetOut(originalOutput);
        }

        Assert.AreEqual((int)SurlExitCode.Ok, exitCode);
        Assert.AreEqual(HelpText.Answer(null).Output, output.ToString());
    }

    [TestMethod]
    public void Main_NullArguments_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = Program.Main(null!); });
    }

    [TestMethod]
    public async Task RunAsync_UnknownOption_WritesTheRefusalAndReturnsFailedInit()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(["--bogus"], output, error, TestContext.CancellationToken);

        Assert.AreEqual((int)SurlExitCode.FailedInit, exitCode);
        StringAssert.StartsWith(error.ToString(), "surl: option --bogus: is unknown" + Environment.NewLine);
    }

    [TestMethod]
    [DataRow(PosixSignal.SIGINT)]
    [DataRow(PosixSignal.SIGTERM)]
    public void CreateStopOnSignal_Signal_CancelsTheDefaultHandlingAndTheStopToken(PosixSignal signal)
    {
        using var stop = new CancellationTokenSource();
        var context = new PosixSignalContext(signal);

        Program.CreateStopOnSignal(stop)(context);

        Assert.IsTrue(context.Cancel);
        Assert.IsTrue(stop.IsCancellationRequested);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunAsync_ServedDirectoryOnAnEphemeralPort_ServesAFileOverHttpAndReturnsOkWhenCancelled()
    {
        var directory = Directory.CreateTempSubdirectory("surl-console-");
        try
        {
            byte[] hello = "hello from surl\n"u8.ToArray();
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "hello.txt"), hello, TestContext.CancellationToken);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
            using var output = new FirstLineWriter();
            using var error = new StringWriter();

            var running = Program.RunAsync(
                ["--directory", directory.FullName, "http://127.0.0.1:0/"], output, error, stop.Token);
            var statusLine = await output.FirstLine.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            var baseUrl = new Uri(statusLine["Listening on ".Length..]);
            byte[] fetched;
            using (var client = new HttpClient())
            {
                fetched = await client.GetByteArrayAsync(new Uri(baseUrl, "hello.txt"), TestContext.CancellationToken);
            }

            await stop.CancelAsync();
            var exitCode = await running;

            StringAssert.StartsWith(statusLine, "Listening on http://127.0.0.1:");
            CollectionAssert.AreEqual(hello, fetched);
            Assert.AreEqual((int)SurlExitCode.Ok, exitCode);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
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
