using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using Surl.Kerberos;
using Surl.Protocol.Abstractions;
using Surl.Protocol.Socks;

namespace Surl.Composition;

[TestClass]
public sealed class SocksGssApiLoginCompositionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ComposeAsync_WithoutAKeytab_HasNoSocksGssApiLogin()
    {
        var services = await StartAndCaptureServicesAsync(new StartTestSeams().Seams, "socks5://127.0.0.1:0");

        Assert.IsNull(services.SocksGssApiLogin);
    }

    [TestMethod]
    public async Task ComposeAsync_WithAKeytab_GivesSocksTheKeytabLoginForTheDefaultService()
    {
        var seams = new StartTestSeams().Seams with { ReadStartFile = _ => AesKeytab() };

        var services = await StartAndCaptureServicesAsync(seams, "--keytab", "socks.keytab", "socks5://127.0.0.1:0");

        Assert.IsNotNull(services.SocksGssApiLogin);
        Assert.AreEqual(SocksGssApiLogin.DefaultService, services.SocksGssApiLogin.Service);
    }

    [TestMethod]
    public async Task ComposeAsync_WithAKeytab_ReadsTheKeytabFileOnce()
    {
        var read = new List<string>();
        var seams = new StartTestSeams().Seams with
        {
            ReadStartFile = path =>
            {
                read.Add(path);
                return AesKeytab();
            },
        };

        await StartAndCaptureServicesAsync(seams, "--keytab", "socks.keytab", "socks5://127.0.0.1:0");

        CollectionAssert.AreEqual(new[] { "socks.keytab" }, read);
    }

    [TestMethod]
    public async Task ComposeAsync_WithAKeytab_GivesSocksAndTheAuthenticationPolicyOneAcceptor()
    {
        var seams = new StartTestSeams().Seams with { ReadStartFile = _ => AesKeytab() };

        var services = await StartAndCaptureServicesAsync(seams, "--keytab", "socks.keytab", "socks5://127.0.0.1:0");

        var acceptor = services.SocksGssApiLogin!.Acceptor;
        Assert.AreSame(services.AuthenticationPolicy.Settings.KerberosAcceptor, acceptor);
        Assert.AreSame(acceptor, services.AuthenticationPolicyFor("http").Settings.KerberosAcceptor);
    }

    [TestMethod]
    public async Task ComposeAsync_TwoInstancesWithOneKeytab_DoNotShareAReplayCache()
    {
        var seams = new StartTestSeams().Seams with { ReadStartFile = _ => AesKeytab() };

        var first = await StartAndCaptureServicesAsync(seams, "--keytab", "socks.keytab", "socks5://127.0.0.1:0");
        var second = await StartAndCaptureServicesAsync(seams, "--keytab", "socks.keytab", "socks5://127.0.0.1:0");

        Assert.AreNotSame(ReplayCacheOf(first.SocksGssApiLogin!.Acceptor), ReplayCacheOf(second.SocksGssApiLogin!.Acceptor));
    }

    // The acceptor shows no replay cache, so the test reads the one field of that type.
    private static KerberosReplayCache ReplayCacheOf(KerberosAcceptor acceptor) =>
        (KerberosReplayCache)typeof(KerberosAcceptor)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(KerberosReplayCache))
            .GetValue(acceptor)!;

    // Starts the command line with one more server, whose registration hands back the services every server is built from.
    private async Task<ComposedProtocolServices> StartAndCaptureServicesAsync(
        SurlServerCompositionSeams seams, params string[] arguments)
    {
        ComposedProtocolServices? captured = null;
        var probe = new AdditionalProtocolServer(
            new ProtocolServerRegistration(
                ["probe"],
                services =>
                {
                    captured = services;
                    return new ProbeServer();
                }),
            []);

        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse(arguments),
            seams with { AdditionalProtocolServers = [probe] },
            TestContext.CancellationToken);

        Assert.IsNull(start.Failure);
        await start.Serving!.DisposeAsync();
        return captured!;
    }

    internal static byte[] AesKeytab()
    {
        byte[] key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        List<byte> body = [];
        body.AddRange(U16(2));
        body.AddRange(Counted("EXAMPLE.COM"));
        body.AddRange(Counted("rcmd"));
        body.AddRange(Counted("host.example.com"));
        body.AddRange(U32(1));
        body.AddRange(U32(0));
        body.Add(1);
        body.AddRange(U16(17));
        body.AddRange(U16(key.Length));
        body.AddRange(key);
        return [0x05, 0x02, .. U32(body.Count), .. body];
    }

    private static byte[] Counted(string text) => [.. U16(text.Length), .. Encoding.ASCII.GetBytes(text)];

    private static byte[] U16(int value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)value);
        return bytes;
    }

    private static byte[] U32(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)value);
        return bytes;
    }

    private sealed class ProbeServer : IConnectionProtocolServer
    {
        public IReadOnlyList<string> Schemes { get; } = ["probe"];

        public Task ServeAsync(IConnection connection, ExchangeContext context) => Task.CompletedTask;
    }
}
