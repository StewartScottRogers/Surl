using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

[TestClass]
public sealed class GcsObjectPropertiesReaderTests
{
    [TestMethod]
    public async Task ServeAsync_HeadOfAnObjectPutOverGs_AnswersXGoogHashWithoutReadingTheFile()
    {
        var harness = new GcsServerHarness(exposure: GcsServerHarness.Writable, persistsIndex: true);
        harness.CreateFolder("photos");
        var put = await harness.SendWithBearerAndBodyAsync("PUT", "/photos/a.txt", "hello world");
        var recording = new UnitTestReadRecordingContentFileSystem(harness.FileSystem);
        var store = new ContentStore(GcsServerHarness.Root, recording, GcsServerHarness.Writable);
        var index = new ObjectMetadataIndex(recording, GcsServerHarness.Root, persists: true);
        await index.LoadAsync(CancellationToken.None);
        var server = new GoogleCloudStorageProtocolServer(store, index, harness.Policy, harness.TimeProvider);
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes($"HEAD /photos/a.txt HTTP/1.1\r\nHost: 127.0.0.1:4443\r\n{GcsServerHarness.BearerField}\r\nConnection: close\r\n\r\n")]);

        await server.ServeAsync(connection, harness.Context());

        var response = Encoding.UTF8.GetString(connection.WrittenBytes);
        var hashField = put.Split("\r\n").Single(line => line.StartsWith("x-goog-hash: ", StringComparison.Ordinal));
        StringAssert.Contains(response, hashField + "\r\n");
        Assert.IsFalse(recording.OpenedForRead.Any(path => path.EndsWith("a.txt", StringComparison.Ordinal)), string.Join(", ", recording.OpenedForRead));
    }
}
