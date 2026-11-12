using Surl.Cli;
using Surl.Content;
using Surl.Extended.Protocol.Amqp0;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    private static readonly string Amqp0ExchangeFileUnderServed =
        Path.Join(Path.GetFullPath("served"), ".surl", Amqp0ExchangeFiles.StateFolderName, Amqp0ExchangeFiles.ExchangesFileName);

    // The file of a broker that declared no exchange: the header and a count of zero.
    private static readonly byte[] EmptyAmqp0ExchangeFile = [.. Amqp0ExchangeFiles.Header, 0, 0, 0, 0];

    [TestMethod]
    public async Task LoadAmqp0ExchangesAsync_WithADataDirectory_HandsTheBrokerItsFileUnderSurl()
    {
        var broker = NewAmqp0Broker();
        var fileSystem = FileSystemWithAmqp0ExchangeFile(EmptyAmqp0ExchangeFile);

        var failureMessage = await SurlServerComposition.LoadAmqp0ExchangesAsync(
            [broker], new SurlCommandLine { DataDirectory = "served" }, fileSystem, TestContext.CancellationToken);

        Assert.IsNull(failureMessage);

        // A broker loads once: a second load proves the first one handed it its files.
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => broker.LoadExchangesAsync(new Amqp0ExchangeFiles(fileSystem, Path.Join(Path.GetFullPath("served"), ".surl")), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadAmqp0ExchangesAsync_WithoutADataDirectory_LeavesTheBrokerInMemory()
    {
        var broker = NewAmqp0Broker();

        var failureMessage = await SurlServerComposition.LoadAmqp0ExchangesAsync(
            [broker], new SurlCommandLine(), new InMemoryContentFileSystem(TimeProvider.System), TestContext.CancellationToken);

        Assert.IsNull(failureMessage);
        await broker.LoadExchangesAsync(
            new Amqp0ExchangeFiles(new InMemoryContentFileSystem(TimeProvider.System), Path.GetFullPath("served")), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task LoadAmqp0ExchangesAsync_AMalformedFile_GivesCouldNotReadNamingTheFile()
    {
        var failureMessage = await SurlServerComposition.LoadAmqp0ExchangesAsync(
            [NewAmqp0Broker()],
            new SurlCommandLine { DataDirectory = "served" },
            FileSystemWithAmqp0ExchangeFile("not an exchange file"u8.ToArray()),
            TestContext.CancellationToken);

        Assert.AreEqual($"(37) Could not read {Amqp0ExchangeFileUnderServed}: {Amqp0ExchangeFiles.MalformedFileReason}", failureMessage);
    }

    [TestMethod]
    public async Task LoadAmqp0ExchangesAsync_NullArguments_Throw()
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => SurlServerComposition.LoadAmqp0ExchangesAsync(null!, new SurlCommandLine(), fileSystem, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => SurlServerComposition.LoadAmqp0ExchangesAsync([], null!, fileSystem, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ComposeAsync_ADataDirectoryWithAMalformedAmqp0ExchangeFile_FailsWithCouldNotReadFile()
    {
        var seams = new StartTestSeams().Seams with
        {
            DataDirectoryFileSystem = FileSystemWithAmqp0ExchangeFile("not an exchange file"u8.ToArray()),
            AreExtensionsTurnedOn = true,
        };

        var start = await SurlServerComposition.ComposeAsync(
            ParseWithAmqp("--directory", "served", "amqp://127.0.0.1:0"), seams, TestContext.CancellationToken);

        Assert.IsNull(start.Serving);
        Assert.AreEqual(SurlExitCode.CouldNotReadFile, start.Failure!.ExitCode);
        Assert.AreEqual(
            $"(37) Could not read {Amqp0ExchangeFileUnderServed}: {Amqp0ExchangeFiles.MalformedFileReason}", start.Failure.Lines.Single());
    }

    [TestMethod]
    public async Task ComposeAsync_ADataDirectoryWithAnAmqp0ExchangeFile_Serves()
    {
        var seams = new StartTestSeams().Seams with
        {
            DataDirectoryFileSystem = FileSystemWithAmqp0ExchangeFile(EmptyAmqp0ExchangeFile),
            AreExtensionsTurnedOn = true,
        };

        var start = await SurlServerComposition.ComposeAsync(
            ParseWithAmqp("--directory", "served", "amqp://127.0.0.1:0"), seams, TestContext.CancellationToken);

        Assert.IsNull(start.Failure);
        Assert.IsNotNull(start.Serving);
        await start.Serving.DisposeAsync();
    }

    private static SurlCommandLine ParseWithAmqp(params string[] args) =>
        CommandLineParser.Parse(args, ["amqp"]).CommandLine ?? throw new AssertFailedException("The command line was refused.");

    private static Amqp0Broker NewAmqp0Broker()
    {
        var services = SurlServerComposition.ComposeUnservedProtocolServices(
            SurlServerComposition.ComposeContentStore(new SurlCommandLine(), TimeProvider.System), TimeProvider.System);
        return new Amqp0Broker(services.AuthenticationPolicy, services.MessageBus);
    }

    private static InMemoryContentFileSystem FileSystemWithAmqp0ExchangeFile(byte[] content)
    {
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        fileSystem.CreateDirectory(Path.GetDirectoryName(Amqp0ExchangeFileUnderServed)!);
        using (var file = fileSystem.CreateFileForAsyncWrite(Amqp0ExchangeFileUnderServed))
        {
            file.Write(content);
        }

        return fileSystem;
    }
}
