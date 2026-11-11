using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port client's <c>coap</c> extension connects to a live <c>surl coap://</c> and
/// <c>coaps://</c> (ADR-0083 decisions 1 to 3, BL-477). Every assertion is a rule of RFC 7252, RFC 7959 or
/// RFC 7641 as ADR-0097 decides it, listed in <c>Documentation/Conformance/Extended/Coap.md</c>; what the
/// client prints is never an expected value beyond the bytes the server was required to send back.
/// Inconclusive when no pinned client is installed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class PinnedCurlPortClientTalksToSurlOverCoapTests
{
    private static readonly byte[] SmallFile = Encoding.UTF8.GetBytes("hello from surl\n");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Description("Coap:RFC7252-5.8.1-1: a confirmable GET of a file is answered 2.05 Content with the file's bytes")]
    public async Task Get_SmallFile_WritesItsBytes()
    {
        await using var surl = await StartAsync("coap", new() { ["hello.txt"] = SmallFile }, "--allow-anonymous");

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, Url(surl, "coap", "/hello.txt"));

        AssertWroteExactly(result, surl, SmallFile);
    }

    [TestMethod]
    [Description("Coap:RFC7959-2.4-1: a file over 1024 bytes is answered in Block2 blocks the client fetches one after another and joins in order")]
    public async Task Get_FileOverOneBlock_IsFetchedBlockByBlockAndJoinedInOrder()
    {
        var large = Enumerable.Range(0, 5000).Select(index => (byte)(index % 251)).ToArray();
        await using var surl = await StartAsync("coap", new() { ["large.bin"] = large }, "--allow-anonymous");

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, Url(surl, "coap", "/large.bin"));

        AssertWroteExactly(result, surl, large);
    }

    [TestMethod]
    [Description("Coap:RFC7252-5.8.3-1: a PUT under --allow-uploads creates the file with the payload, and a GET then reads it back")]
    public async Task Put_UnderAllowUploads_CreatesTheFile()
    {
        await using var surl = await StartAsync("coap", [], "--allow-anonymous", "--allow-uploads");

        var put = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-X", "PUT", "-d", "uploaded\n", Url(surl, "coap", "/new.txt"));
        var get = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, Url(surl, "coap", "/new.txt"));

        Assert.AreEqual(0, put.ExitCode, Diagnostics(put, surl));
        AssertWroteExactly(get, surl, Encoding.UTF8.GetBytes("uploaded\n"));
        Assert.AreEqual("uploaded\n", await File.ReadAllTextAsync(Path.Combine(surl.ServedDirectory!, "new.txt"), TestContext.CancellationToken));
    }

    [TestMethod]
    [Description("Coap:RFC7252-5.8.3-1, Coap:ADR0097-1-1: without --allow-uploads a PUT is refused and no file is made")]
    public async Task Put_WithoutAllowUploads_IsRefusedAndNothingIsWritten()
    {
        await using var surl = await StartAsync("coap", [], "--allow-anonymous");

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-X", "PUT", "-i", "-d", "uploaded\n", Url(surl, "coap", "/new.txt"));

        StringAssert.Contains(Encoding.UTF8.GetString(result.StandardOutput.ToArray()) + result.StandardError, "4.05", Diagnostics(result, surl));
        Assert.IsFalse(File.Exists(Path.Combine(surl.ServedDirectory!, "new.txt")), Diagnostics(result, surl));
    }

    [TestMethod]
    [Description("Coap:RFC7641-4.2-1, Coap:RFC7641-4.3.1-1: an observer gets the representation at registration and a notification after another client's PUT changes it")]
    public async Task Observe_ThenAnotherClientPuts_ReceivesTheNewRepresentation()
    {
        await using var surl = await StartAsync("coap", new() { ["watched.txt"] = Encoding.UTF8.GetBytes("first\n") }, "--allow-anonymous", "--allow-uploads");
        var observing = PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "--coap-observe", "--max-time", "8", Url(surl, "coap", "/watched.txt"));

        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.CancellationToken);
        var put = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-X", "PUT", "-d", "second\n", Url(surl, "coap", "/watched.txt"));
        var observed = await observing;

        Assert.AreEqual(0, put.ExitCode, Diagnostics(put, surl));
        var text = Encoding.UTF8.GetString(observed.StandardOutput.ToArray());
        StringAssert.Contains(text, "first\n", Diagnostics(observed, surl));
        StringAssert.Contains(text, "second\n", Diagnostics(observed, surl));
        Assert.IsLessThan(text.IndexOf("second\n", StringComparison.Ordinal), text.IndexOf("first\n", StringComparison.Ordinal), text);
    }

    [TestMethod]
    [Description("Coap:RFC7252-9.1.3.3-1, Coap:RFC7252-6.2-1: coaps in certificate mode completes the DTLS handshake and answers the GET inside it")]
    public async Task Get_OverDtlsWithACertificate_WritesTheFilesBytes()
    {
        await using var surl = await StartAsync("coaps", new() { ["hello.txt"] = SmallFile }, "--allow-anonymous", "--self-signed");

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-k", Url(surl, "coaps", "/hello.txt"));

        AssertWroteExactly(result, surl, SmallFile);
    }

    [TestMethod]
    [Description("Coap:RFC7252-9.1.3.1-1, Coap:ADR0087-3-1: a coaps server that has only a pre-shared-key account refuses the certificate-mode client's handshake, so nothing is read")]
    public async Task Get_OverDtlsWhenTheServerHasOnlyAPreSharedKey_IsRefusedAndNothingIsWritten()
    {
        await using var surl = await StartAsync("coaps", new() { ["hello.txt"] = SmallFile }, "--user", "alice:secret");

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(TestContext, "-k", Url(surl, "coaps", "/hello.txt"));

        Assert.AreNotEqual(0, result.ExitCode, Diagnostics(result, surl));
        Assert.IsEmpty(result.StandardOutput.ToArray(), Diagnostics(result, surl));
    }

    private Task<SurlOnLoopback> StartAsync(string scheme, Dictionary<string, byte[]> files, params string[] options) =>
        SurlOnLoopback.StartAsync(scheme, files, [], options, TestContext.CancellationToken);

    private static string Url(SurlOnLoopback surl, string scheme, string path) =>
        $"{scheme}://127.0.0.1:{surl.BaseUrl.Port}{path}";

    private static string Diagnostics(CurlPortClientRunResult result, SurlOnLoopback surl) =>
        $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";

    private static void AssertWroteExactly(CurlPortClientRunResult result, SurlOnLoopback surl, byte[] expected)
    {
        Assert.AreEqual(0, result.ExitCode, Diagnostics(result, surl));
        CollectionAssert.AreEqual(expected, result.StandardOutput.ToArray(), Diagnostics(result, surl));
    }
}
