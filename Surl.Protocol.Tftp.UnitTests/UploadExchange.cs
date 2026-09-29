using System.Net;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Tftp;

/// <summary>
/// Drives the TFTP server through write requests: a content store over an in-memory file
/// system that accepts uploads, a scripted flow, and the recordings to replay.
/// </summary>
internal static class UploadExchange
{
    public static readonly string Root = Path.Join(Path.GetTempPath(), "surl-tftp-upload-tests");

    public static readonly string UploadPath = Path.Join(Root, "up.txt");

    public static readonly byte[] AccessViolation = [0, 5, 0, 2, .. "Access violation"u8, 0];

    public static readonly byte[] IllegalOperation = [0, 5, 0, 4, .. "Illegal TFTP operation"u8, 0];

    public static InMemoryContentFileSystem FileSystem() => new InMemoryContentFileSystem().AddDirectory(Root);

    public static ContentStore Store(InMemoryContentFileSystem fileSystem, bool allowUploads = true, long maxUploadBytes = ContentExposureOptions.DefaultMaxUploadBytes) =>
        new(Root, fileSystem, new ContentExposureOptions { AllowUploads = allowUploads, MaxUploadBytes = maxUploadBytes });

    public static ExchangeContext Context(IExchangeLog log, TimeProvider clock, ExchangeLimits? limits = null, CancellationToken cancellationToken = default) => new(
        1,
        new ListenUrl("tftp", "127.0.0.1", ScriptedDatagramFlow.ListenPort).WithBoundPort(ScriptedDatagramFlow.ListenPort),
        new IPEndPoint(IPAddress.Loopback, ScriptedDatagramFlow.ListenPort),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        clock,
        cancellationToken)
    {
        Limits = limits ?? ExchangeLimits.Default,
    };

    public static async Task<(ScriptedDatagramFlow Flow, RecordingExchangeLog Log)> ServeAsync(
        ContentStore store,
        byte[] firstDatagram,
        IEnumerable<ScriptedDatagramFlow.Step> script,
        ExchangeLimits? limits = null)
    {
        var clock = new ManualTimeProvider();
        var flow = new ScriptedDatagramFlow(firstDatagram, clock, script);
        var log = new RecordingExchangeLog();

        await new TftpProtocolServer(store).ServeAsync(flow, Context(log, clock, limits));

        return (flow, log);
    }

    /// <summary>
    /// Serves the recording's first datagram and plays every later datagram curl sent.
    /// </summary>
    public static Task<(ScriptedDatagramFlow Flow, RecordingExchangeLog Log)> ReplayAsync(ContentStore store, string caseName, ExchangeLimits? limits = null)
    {
        var curl = RecordedFixture.CurlDatagrams(caseName);
        return ServeAsync(store, curl[0], curl.Skip(1).Select(ScriptedDatagramFlow.Datagram), limits);
    }

    public static void AssertSent(IReadOnlyList<byte[]> expected, ScriptedDatagramFlow flow)
    {
        Assert.HasCount(expected.Count, flow.Sent);
        for (var index = 0; index < expected.Count; index++)
        {
            CollectionAssert.AreEqual(expected[index], flow.Sent[index].Bytes, $"datagram {index}");
        }
    }

    public static byte[] WriteRequest(string fileName, params string[] options)
    {
        var text = new StringBuilder(fileName).Append('\0').Append("octet").Append('\0');
        foreach (var field in options)
        {
            text.Append(field).Append('\0');
        }

        return [0, 2, .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    public static byte[] Ack(ushort blockNumber) => [0, 4, (byte)(blockNumber >> 8), (byte)blockNumber];

    public static byte[] Data(ushort blockNumber, byte[] payload) => [0, 3, (byte)(blockNumber >> 8), (byte)blockNumber, .. payload];
}
