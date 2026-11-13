using System.Text;

namespace Surl.Conformance;

/// <summary>
/// The pinned Curl port's <c>grpc</c> client against a live <c>surl grpc://</c> and <c>grpcs://</c> (BL-446,
/// ADR-0083 decisions 1 to 3): a unary call whose types the client learns by server reflection, the same call with
/// a <c>.proto</c> file, a call to a method the server does not have, and a call with no login. Each expected value
/// comes from PROTOCOL-HTTP2.md and statuscodes.md as <c>Documentation/Conformance/Extended/Grpc.md</c> lists them -
/// <c>Echo</c> replies once with <c>message</c> copied, an unknown method is <c>UNIMPLEMENTED</c> (12), and a call
/// without credentials is <c>UNAUTHENTICATED</c> (16) - never from what the port printed beyond the status the
/// server was required to send. Inconclusive when the pinned client is absent.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CurlPortClientCallsMethodsOnSurlOverGrpcTests
{
    private const string EchoMethod = "surl.test.Echo/Echo";
    private const string BasicLogin = "Authorization: Basic YWxpY2U6c2VjcmV0"; // alice:secret; the port ignores -u on grpc, so the header is sent by hand
    private const string EchoRequest = """{"message":"hello"}""";
    private const string EchoProto = """
        syntax = "proto3";
        package surl.test;
        service Echo { rpc Echo (EchoRequest) returns (EchoReply); }
        message EchoRequest { string message = 1; uint32 count = 2; int32 status_code = 3; string status_message = 4; uint32 delay_milliseconds = 5; }
        message EchoReply { string message = 1; uint32 index = 2; }
        """;

    private IsolatedCurlHome localFiles = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void CreateLocalFiles() => localFiles = new IsolatedCurlHome();

    [TestCleanup]
    public void DeleteLocalFiles() => localFiles.Dispose();

    [TestMethod]
    [Description("Grpc:Reflection-4, Grpc:Reflection-5, Grpc:Surl-Echo-1, Grpc:HTTP2-Responses-3: with no .proto the client describes Echo by reflection, calls it, and gets the message back with grpc-status 0")]
    public async Task Echo_DescribedByReflection_RepliesWithTheMessage()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync("grpc", ["--allow-anonymous"], TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-m", "20", "-d", EchoRequest, Url(surl, "grpc"));

        AssertEchoed(result, surl);
    }

    [TestMethod]
    [Description("Grpc:HTTP2-Requests-3, Grpc:Surl-Echo-1, Grpc:Protobuf-1: with a .proto file the client calls Echo without reflection and gets the message back")]
    public async Task Echo_DescribedByAProtoFile_RepliesWithTheMessage()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync("grpc", ["--allow-anonymous"], TestContext.CancellationToken);
        var proto = await localFiles.WriteFileAsync("echo.proto", Encoding.UTF8.GetBytes(EchoProto), TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-m", "20", "--grpc-proto", proto, "-d", EchoRequest, Url(surl, "grpc"));

        AssertEchoed(result, surl);
    }

    [TestMethod]
    [Description("Grpc:HTTP2-Security-2, Grpc:HTTP2-Requests-3, Grpc:Surl-Echo-1: grpcs is the same server inside TLS with ALPN h2, and Echo replies over it")]
    public async Task Echo_OverTls_RepliesWithTheMessage()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync(
            "grpcs", ["--allow-anonymous", "--self-signed"], TestContext.CancellationToken);
        var proto = await localFiles.WriteFileAsync("echo.proto", Encoding.UTF8.GetBytes(EchoProto), TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-k", "-m", "20", "--grpc-proto", proto, "-d", EchoRequest, Url(surl, "grpcs"));

        AssertEchoed(result, surl);
    }

    [TestMethod]
    [Description("Grpc:HTTP2-Requests-5, Grpc:Status-1: a method the server does not have is UNIMPLEMENTED (12), which the client reports as a failed transfer")]
    public async Task UnknownMethod_IsUnimplemented()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync("grpc", ["--allow-anonymous"], TestContext.CancellationToken);
        var proto = await localFiles.WriteFileAsync(
            "missing.proto",
            Encoding.UTF8.GetBytes(EchoProto.Replace("rpc Echo (", "rpc Missing (", StringComparison.Ordinal)),
            TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-m", "20", "--grpc-proto", proto, "-d", EchoRequest, Url(surl, "grpc", "surl.test.Echo/Missing"));

        AssertRefused(result, surl, "grpc-status 12");
    }

    [TestMethod]
    [Description("Grpc:Status-5, Grpc:Surl-Auth-1: with no --allow-anonymous a call with no credentials is UNAUTHENTICATED (16), judged before the method is looked up")]
    public async Task Call_WithoutCredentials_IsUnauthenticated()
    {
        await using var surl = await SurlOnLoopback.StartInMemoryAsync("grpc", ["--user", "alice:secret"], TestContext.CancellationToken);
        var proto = await localFiles.WriteFileAsync("echo.proto", Encoding.UTF8.GetBytes(EchoProto), TestContext.CancellationToken);

        var result = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-m", "20", "--grpc-proto", proto, "-d", EchoRequest, Url(surl, "grpc"));

        AssertRefused(result, surl, "grpc-status 16");
    }

    [TestMethod]
    [Description("Grpc:Surl-Auth-3, Grpc:Surl-Auth-2: on grpc, which is plain text, credentials are refused (a Forbidden verdict, PERMISSION_DENIED) unless --allow-plaintext-auth, and accepted with it")]
    public async Task Credentials_OverPlainText_AreRefusedUnlessPlaintextAuthIsAllowed()
    {
        var proto = await localFiles.WriteFileAsync("echo.proto", Encoding.UTF8.GetBytes(EchoProto), TestContext.CancellationToken);
        await using var refusing = await SurlOnLoopback.StartInMemoryAsync("grpc", ["--user", "alice:secret"], TestContext.CancellationToken);
        await using var allowing = await SurlOnLoopback.StartInMemoryAsync(
            "grpc", ["--user", "alice:secret", "--allow-plaintext-auth"], TestContext.CancellationToken);

        var refused = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-m", "20", "-H", BasicLogin, "--grpc-proto", proto, "-d", EchoRequest, Url(refusing, "grpc"));
        var served = await PinnedCurlPortClientOnThisMachine.RunAsync(
            TestContext, "-sS", "-m", "20", "-H", BasicLogin, "--grpc-proto", proto, "-d", EchoRequest, Url(allowing, "grpc"));

        AssertRefused(refused, refusing, "grpc-status 7");
        AssertEchoed(served, allowing);
    }

    private static string Url(SurlOnLoopback surl, string scheme, string method = EchoMethod) =>
        $"{scheme}://127.0.0.1:{surl.BaseUrl.Port}/{method}";

    private static void AssertEchoed(CurlPortClientRunResult result, SurlOnLoopback surl)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreEqual(0, result.ExitCode, diagnostics);
        StringAssert.Contains(Encoding.UTF8.GetString(result.StandardOutput), "\"message\": \"hello\"", diagnostics);
    }

    private static void AssertRefused(CurlPortClientRunResult result, SurlOnLoopback surl, string expectedStatus)
    {
        var diagnostics = $"{result}; stderr: {result.StandardError}; surl: {surl.Log}";
        Assert.AreNotEqual(0, result.ExitCode, diagnostics);
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(result.StandardOutput).Trim(), diagnostics);
        StringAssert.Contains(result.StandardError, expectedStatus, diagnostics);
    }
}
