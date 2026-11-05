using Surl.Content;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Where the Blob answerer finds a blob's properties when the object metadata index has none.
/// </summary>
[TestClass]
public sealed class AzureBlobRequestAnswererTests
{
    [TestMethod]
    public async Task AnswerAsync_BlobInAShare_AnswersTheIndexDefaults()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(AzureServerHarness.Now));
        AzureServerHarness.WriteFile(shareFileSystem, "a.txt", "hello world");
        var harness = new AzureServerHarness(shares: [new ContentShare("shared", AzureServerHarness.Root, shareFileSystem)]);

        var response = await harness.SendWithBearerAsync("GET", "/shared/a.txt");

        StringAssert.Contains(response, "Content-Type: application/octet-stream\r\nContent-MD5: XrY7u+Ae7tCTyyK7j1rNww==\r\nETag: \"5eb63bbbe01eeed093cb22bb8f5acdc3\"\r\n");
        StringAssert.EndsWith(response, "hello world");
    }

    [TestMethod]
    public async Task AnswerAsync_IndexThatCannotBeWritten_ServesTheBlobAndNotesTheFailure()
    {
        var harness = new AzureServerHarness(maxTotalBytes: 11, persistsIndex: true);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        StringAssert.EndsWith(response, "hello world");
        Assert.IsTrue(harness.Log.Notes.Any(note => note.StartsWith("The object metadata index could not be written: ", StringComparison.Ordinal)));
    }
}
