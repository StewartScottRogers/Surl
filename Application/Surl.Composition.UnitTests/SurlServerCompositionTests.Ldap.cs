using System.Formats.Asn1;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Ldap;

namespace Surl.Composition;

public sealed partial class SurlServerCompositionTests
{
    // Bind as alice (simple, version 3), then a base search of dc=example,dc=com with timeLimit 1, then an unbind;
    // the bytes upstream curl sends for a search (Surl.Protocol.Ldap.UnitTests Fixtures/simple-bind-base-search)
    // with the search's timeLimit changed from 0 to 1.
    private const string BindThenSearchWithTimeLimitOneThenUnbind =
        "30840000001B0201016084000000120201030405616C69636580067365637265743084000000"
        + "3E020102638400000035041164633D6578616D706C652C64633D636F6D0A01000A0100020100"
        + "020101010100870B6F626A656374436C61737330840000000030840000000502010342" + "00";

    [TestMethod]
    public async Task ComposeAsync_LdapSearchWithTimeLimitOne_IsCutShortOnlyOnceTheInstancesClockPassesIt()
    {
        var stoodStill = await SearchWithClockStepAsync(TimeSpan.Zero);
        var movedOn = await SearchWithClockStepAsync(TimeSpan.FromSeconds(2));

        CollectionAssert.AreEqual(new[] { (1, 0), (4, -1), (5, 0) }, stoodStill);
        CollectionAssert.AreEqual(new[] { (1, 0), (5, 3) }, movedOn);
    }

    private async Task<(int Tag, int ResultCode)[]> SearchWithClockStepAsync(TimeSpan step)
    {
        var clock = new SteppingTimeProvider();
        var fileSystem = new InMemoryContentFileSystem(TimeProvider.System);
        var folder = Path.Join(Path.GetFullPath("served"), ".surl", "ldap");
        fileSystem.CreateDirectory(folder);
        await using (var file = fileSystem.CreateFileForAsyncWrite(Path.Join(folder, LdapDirectoryFile.FileName)))
        {
            await file.WriteAsync(Encoding.UTF8.GetBytes("dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n"), TestContext.CancellationToken);
        }

        var connection = new FakeConnection(Convert.FromHexString(BindThenSearchWithTimeLimitOneThenUnbind));
        var seams = new StartTestSeams(new FakeListenerFactory { ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>> { ["ldap"] = Task.FromResult(connection) } });
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--allow-plaintext-auth", "-u", "alice:secret", "--directory", "served", "ldap://127.0.0.1:0"),
            seams.Seams with { TimeProvider = clock, DataDirectoryFileSystem = fileSystem },
            TestContext.CancellationToken);
        Assert.IsNull(start.Failure);
        await using var serving = start.Serving!;
        clock.Step = step;

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log), stop.Token);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        return ReadLdapResponses(connection.WrittenBytes);
    }

    // Each response's protocolOp tag and result code (-1 for an entry, which has none).
    private static (int Tag, int ResultCode)[] ReadLdapResponses(byte[] written)
    {
        var responses = new List<(int, int)>();
        var reader = new AsnReader(written, AsnEncodingRules.BER);
        while (reader.HasData)
        {
            var message = reader.ReadSequence();
            message.ReadInteger();
            var tag = message.PeekTag();
            var operation = message.ReadSequence(tag);
            responses.Add((tag.TagValue, tag.TagValue == 4 ? -1 : operation.ReadEnumeratedBytes().Span[0]));
        }

        return [.. responses];
    }
}
