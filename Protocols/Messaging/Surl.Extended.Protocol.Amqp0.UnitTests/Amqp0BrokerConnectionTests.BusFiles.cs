using Surl.Content;
using Surl.MessageBus;
using static Surl.Extended.Protocol.Amqp0.UnitTests.Amqp0ChannelTestBytes;

namespace Surl.Extended.Protocol.Amqp0.UnitTests;

/// <summary>A durable queue's changes are saved to the bus's files (ADR-0112 decision 6; BL-967).</summary>
public sealed partial class Amqp0BrokerConnectionTests
{
    private const byte DurableQueue = 2;

    [TestMethod]
    public async Task Publish_ToADurableQueue_IsHeldByASecondBusLoadedFromTheSameFiles()
    {
        var fileSystem = new InMemoryContentFileSystem(new ManualTimeProvider());
        var first = NewBus();
        await first.LoadAsync(new MessageBusFiles(fileSystem, SurlFolderPath), TestContext.CancellationToken);

        await ServeAsync(NewBroker(first), QueueDeclare(1, "orders", DurableQueue), PublishText(1, string.Empty, "orders", "hello"));
        var second = NewBus();
        await second.LoadAsync(new MessageBusFiles(fileSystem, SurlFolderPath), TestContext.CancellationToken);

        Assert.AreEqual(1, ReadyCount(second, "orders"));
    }

    [TestMethod]
    public async Task Publish_WhenTheBusFilesCannotBeWritten_IsNotedAndTheConnectionGoesOn()
    {
        var fileSystem = new UnitTestFailingContentFileSystem();
        var bus = NewBus();
        await bus.LoadAsync(new MessageBusFiles(fileSystem, SurlFolderPath), TestContext.CancellationToken);
        fileSystem.FailWrite = true;
        fileSystem.FailDelete = true;

        var (written, log) = await ServeLoggedAsync(
            NewBroker(bus), DefaultTuneOk, QueueDeclare(1, "orders", DurableQueue), PublishText(1, string.Empty, "orders", "hello"), QueueDeclare(1, "after"));

        AssertAnswered(written, QueueDeclareOk(1, "orders"), QueueDeclareOk(1, "after"));
        Assert.IsTrue(log.Notes.Any(note => note.StartsWith("The message bus could not save its files (", StringComparison.Ordinal)));
        Assert.AreEqual(1, ReadyCount(bus, "orders"));
    }
}
