using Surl.Content;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0TestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>The headers exchange and durable exchanges across restarts (ADR-0112 decisions 5 and 6; BL-732).</summary>
public sealed partial class Amqp0BrokerConnectionTests
{
    private static readonly Amqp0Field MatchAll = new("x-match", Amqp0FieldValue.LongString("all"));
    private static readonly Amqp0Field MatchAny = new("x-match", Amqp0FieldValue.LongString("any"));
    private static readonly Amqp0Field FormatPdf = new("format", Amqp0FieldValue.LongString("pdf"));
    private static readonly Amqp0Field TypeReport = new("type", Amqp0FieldValue.LongString("report"));

    [TestMethod]
    public async Task ExchangeDeclare_AmqHeadersAndAmqMatch_ArePresentFromStart()
    {
        var written = await ServeAsync(
            NewBroker(NewBus()),
            ExchangeDeclare(1, "amq.headers", "headers", bits: 1),
            ExchangeDeclare(1, "amq.match", "headers", bits: 1),
            ExchangeDeclare(1, "amq.match", "headers", bits: 2),
            ExchangeDeclare(1, "reports", "headers"));

        AssertAnswered(written, ExchangeDeclareOk(1), ExchangeDeclareOk(1), ExchangeDeclareOk(1), ExchangeDeclareOk(1));
    }

    [TestMethod]
    public async Task Publish_ToAHeadersExchangeWithXMatchAll_ReachesOnlyTheQueueWhoseEveryEntryMatches()
    {
        var bus = NewBus();

        await ServeAsync(
            NewBroker(bus),
            QueueDeclare(1, "pdf-reports"),
            QueueDeclare(1, "pdfs"),
            QueueBind(1, "pdf-reports", "amq.headers", string.Empty, table: EncodedTable(MatchAll, FormatPdf, TypeReport)),
            QueueBind(1, "pdfs", "amq.headers", string.Empty, table: EncodedTable(FormatPdf)),
            PublishWithHeaders("amq.headers", FormatPdf),
            PublishWithHeaders("amq.headers", FormatPdf, TypeReport));

        Assert.AreEqual(1, ReadyCount(bus, "pdf-reports"));
        Assert.AreEqual(2, ReadyCount(bus, "pdfs"));
    }

    [TestMethod]
    public async Task Publish_ToAHeadersExchangeWithXMatchAny_ReachesTheQueueWhenOneEntryMatches()
    {
        var bus = NewBus();

        await ServeAsync(
            NewBroker(bus),
            ExchangeDeclare(1, "reports", "headers"),
            QueueDeclare(1, "either"),
            QueueBind(1, "either", "reports", string.Empty, table: EncodedTable(MatchAny, FormatPdf, TypeReport)),
            PublishWithHeaders("reports", TypeReport),
            PublishWithHeaders("reports", new Amqp0Field("format", Amqp0FieldValue.LongString("zip"))),
            PublishText(1, "reports", string.Empty, "no headers"));

        Assert.AreEqual(1, ReadyCount(bus, "either"));
    }

    [TestMethod]
    public async Task QueueUnbind_FromAHeadersExchange_RemovesOnlyTheBindingWithTheSameArguments()
    {
        var bus = NewBus();

        await ServeAsync(
            NewBroker(bus),
            QueueDeclare(1, "q"),
            QueueBind(1, "q", "amq.match", string.Empty, table: EncodedTable(FormatPdf)),
            QueueBind(1, "q", "amq.match", string.Empty, table: EncodedTable(TypeReport)),
            QueueUnbind(1, "q", "amq.match", string.Empty, table: EncodedTable(FormatPdf)),
            PublishWithHeaders("amq.match", FormatPdf),
            PublishWithHeaders("amq.match", TypeReport));

        Assert.AreEqual(1, ReadyCount(bus, "q"));
    }

    [TestMethod]
    public async Task QueueBind_ToAHeadersExchangeWithAnotherXMatch_ClosesTheChannelWith406()
    {
        var table = EncodedTable(new Amqp0Field("x-match", Amqp0FieldValue.LongString("some")));

        var written = await ServeAsync(NewBroker(NewBus()), QueueDeclare(1, "q"), QueueBind(1, "q", "amq.headers", string.Empty, table: table), ChannelCloseOk(1));

        AssertAnswered(written, QueueDeclareOk(1, "q"), BrokerChannelClose(1, 406, "PRECONDITION_FAILED - invalid x-match", 50, 20));
    }

    [TestMethod]
    public async Task LoadExchanges_AfterARestart_KeepsTheDurableDeclaredExchangeAndItsBindingsButNotTheOthers()
    {
        var fileSystem = new InMemoryContentFileSystem(new ManualTimeProvider());
        var bus = NewBus();
        var first = NewBroker(bus);
        await first.LoadExchangesAsync(FilesOn(fileSystem), TestContext.CancellationToken);
        await ServeAsync(
            first,
            ExchangeDeclare(1, "reports", "headers", bits: 2),
            ExchangeDeclare(1, "scratch", "direct"),
            QueueDeclare(1, "pdfs"),
            QueueBind(1, "pdfs", "reports", string.Empty, table: EncodedTable(MatchAny, FormatPdf)),
            QueueBind(1, "pdfs", "scratch", "k"));
        var second = NewBroker(bus);

        await second.LoadExchangesAsync(FilesOn(fileSystem), TestContext.CancellationToken);
        var written = await ServeAsync(
            second,
            ExchangeDeclare(1, "reports", "headers", bits: 1),
            PublishWithHeaders("reports", FormatPdf),
            ExchangeDeclare(1, "scratch", "direct", bits: 1),
            ChannelCloseOk(1));

        AssertAnswered(written, ExchangeDeclareOk(1), BrokerChannelClose(1, 404, "NOT_FOUND - no exchange", 40, 10));
        Assert.AreEqual(1, ReadyCount(bus, "pdfs"));
    }

    [TestMethod]
    public async Task LoadExchanges_AfterTheDurableExchangeAndItsQueueWereDeleted_HoldsNeither()
    {
        var fileSystem = new InMemoryContentFileSystem(new ManualTimeProvider());
        var bus = NewBus();
        var first = NewBroker(bus);
        await first.LoadExchangesAsync(FilesOn(fileSystem), TestContext.CancellationToken);
        await ServeAsync(
            first,
            ExchangeDeclare(1, "kept", "fanout", bits: 2),
            ExchangeDeclare(1, "gone", "fanout", bits: 2),
            QueueDeclare(1, "q"),
            QueueBind(1, "q", "kept", string.Empty),
            QueueDelete(1, "q"),
            ExchangeDelete(1, "gone"));

        var files = FilesOn(fileSystem);
        var persisted = await files.ReadAsync(Amqp0ExchangeTable.MaxDeclaredExchanges, Amqp0ExchangeTable.MaxBindings, TestContext.CancellationToken);

        Assert.AreEqual("kept", persisted.Single().Name);
        Assert.IsEmpty(persisted.Single().Bindings);
    }

    [TestMethod]
    public async Task LoadExchanges_MalformedFile_IsRefusedWithTheFilePathForExit37()
    {
        var fileSystem = new InMemoryContentFileSystem(new ManualTimeProvider());
        var files = FilesOn(fileSystem);
        await WriteFileAsync(fileSystem, files, "SURL-AMQP0-EXCHANGES-0\n"u8.ToArray());

        var failure = await Assert.ThrowsExactlyAsync<Amqp0ExchangeLoadException>(() => NewBroker(NewBus()).LoadExchangesAsync(files, TestContext.CancellationToken));

        Assert.AreEqual(files.ExchangesPath, failure.FilePath);
        Assert.AreEqual(Amqp0ExchangeFiles.MalformedFileReason, failure.Message);
    }

    [TestMethod]
    public async Task LoadExchanges_UnreadableFile_IsRefusedWithTheReadsMessage()
    {
        var fileSystem = new UnitTestFailingContentFileSystem();
        var files = new Amqp0ExchangeFiles(fileSystem, SurlFolderPath);
        await WriteFileAsync(fileSystem, files, Amqp0ExchangeFiles.Encode([]));
        fileSystem.FailRead = true;

        var failure = await Assert.ThrowsExactlyAsync<Amqp0ExchangeLoadException>(() => NewBroker(NewBus()).LoadExchangesAsync(files, TestContext.CancellationToken));

        Assert.AreEqual("The read failed.", failure.Message);
    }

    [TestMethod]
    public async Task LoadExchanges_TwiceOrAfterADeclare_IsInvalid()
    {
        var fileSystem = new InMemoryContentFileSystem(new ManualTimeProvider());
        var loaded = NewBroker(NewBus());
        await loaded.LoadExchangesAsync(FilesOn(fileSystem), TestContext.CancellationToken);
        var declared = NewBroker(NewBus());
        await ServeAsync(declared, ExchangeDeclare(1, "x", "direct"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => loaded.LoadExchangesAsync(FilesOn(fileSystem), TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => declared.LoadExchangesAsync(FilesOn(fileSystem), TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => declared.LoadExchangesAsync(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExchangeDeclare_WhenTheFileCannotBeWritten_IsNotedAndWrittenByTheNextSave()
    {
        var fileSystem = new UnitTestFailingContentFileSystem { FailWrite = true, FailDelete = true };
        var files = new Amqp0ExchangeFiles(fileSystem, SurlFolderPath);
        var broker = NewBroker(NewBus());
        await broker.LoadExchangesAsync(files, TestContext.CancellationToken);

        var (written, log) = await ServeLoggedAsync(broker, DefaultTuneOk, ExchangeDeclare(1, "orders", "direct", bits: 2));
        fileSystem.FailWrite = false;
        await ServeAsync(broker, QueueDeclare(1, "q"));

        AssertAnswered(written, ExchangeDeclareOk(1));
        CollectionAssert.Contains(log.Notes.ToList(), "The exchange file was not written: The write failed.");
        Assert.HasCount(1, fileSystem.DeletedFiles);
        var persisted = await files.ReadAsync(Amqp0ExchangeTable.MaxDeclaredExchanges, Amqp0ExchangeTable.MaxBindings, TestContext.CancellationToken);
        Assert.AreEqual("orders", persisted.Single().Name);
    }

    private static int ReadyCount(MessageBus.MessageBus bus, string queue)
    {
        using var connection = bus.Connect();
        return connection.InspectQueue(queue).ReadyMessageCount;
    }

    private static string SurlFolderPath => Path.Join(InMemoryContentFileSystem.RootPath, ".surl");

    private static Amqp0ExchangeFiles FilesOn(IContentFileSystem fileSystem) => new(fileSystem, SurlFolderPath);

    private static async Task WriteFileAsync(IContentFileSystem fileSystem, Amqp0ExchangeFiles files, byte[] bytes)
    {
        fileSystem.CreateDirectory(files.StateFolderPath);
        await using var stream = fileSystem.CreateFileForAsyncWrite(files.ExchangesPath);
        await stream.WriteAsync(bytes);
    }

    private static byte[] EncodedTable(params Amqp0Field[] fields)
    {
        var writer = new Amqp0WireWriter();
        writer.WriteFieldTable(new(fields));
        return writer.ToArray();
    }

    private static byte[] PublishWithHeaders(string exchange, params Amqp0Field[] headers)
    {
        var (flags, list) = new Amqp0BasicProperties { Headers = new(headers) }.Encode();
        return Concat(Publish(1, exchange, string.Empty), ContentHeader(1, 1, flags, list), Body(1, "x"));
    }
}
