using Surl.Cli;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

[TestClass]
public sealed class ContentStoreSweepCompositionTests
{
    private static readonly DateTimeOffset Written = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly string AbandonedFile =
        Path.Join(InMemoryContentFileSystem.RootPath, ".surl-upload-" + new string('a', 32));

    [TestMethod]
    public void ComposeContentStore_WithADataDirectoryAndASweepClock_RemovesAnOldAbandonedUploadFile()
    {
        var fileSystem = FileSystemHoldingTheAbandonedFile();
        var commandLine = new SurlCommandLine { DataDirectory = InMemoryContentFileSystem.RootPath };

        SurlServerComposition.ComposeContentStore(commandLine, fileSystem, DiscardingSurlEventPublisher.Instance, EmptyFaultPlan.Instance, [], [], LaterClock());

        Assert.AreEqual(ContentEntryKind.None, fileSystem.GetEntryKind(AbandonedFile));
    }

    [TestMethod]
    public void ComposeContentStore_WithoutADataDirectory_SweepsNothing()
    {
        var fileSystem = FileSystemHoldingTheAbandonedFile();

        SurlServerComposition.ComposeContentStore(new SurlCommandLine(), fileSystem, DiscardingSurlEventPublisher.Instance, EmptyFaultPlan.Instance, [], [], LaterClock());

        Assert.AreEqual(ContentEntryKind.File, fileSystem.GetEntryKind(AbandonedFile));
    }

    [TestMethod]
    public void ComposeContentStore_WithADataDirectoryAndNoSweepClock_SweepsNothing()
    {
        var fileSystem = FileSystemHoldingTheAbandonedFile();
        var commandLine = new SurlCommandLine { DataDirectory = InMemoryContentFileSystem.RootPath };

        SurlServerComposition.ComposeContentStore(commandLine, fileSystem, DiscardingSurlEventPublisher.Instance, EmptyFaultPlan.Instance, [], []);

        Assert.AreEqual(ContentEntryKind.File, fileSystem.GetEntryKind(AbandonedFile));
    }

    private static InMemoryContentFileSystem FileSystemHoldingTheAbandonedFile()
    {
        var fileSystem = new InMemoryContentFileSystem(new FixedClock(Written));
        using (var stream = fileSystem.CreateFileForAsyncWrite(AbandonedFile))
        {
            stream.WriteByte(1);
        }

        return fileSystem;
    }

    private static FixedClock LaterClock() => new(Written + ContentStore.AbandonedUploadAge + TimeSpan.FromHours(1));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
