using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

[TestClass]
public sealed class ArrivalOrderedReceivedLogTests
{
    [TestMethod]
    public void Bytes_HalvesGrowOneAfterAnother_AreInArrivalOrder()
    {
        var first = new GrowingHalf();
        var second = new GrowingHalf();
        var log = new ArrivalOrderedReceivedLog([first.Snapshot, second.Snapshot]);

        first.Add("a1 ");
        log.Look();
        second.Add("b1 ");
        log.Look();
        first.Add("a2");

        Assert.AreEqual("a1 b1 a2", Encoding.ASCII.GetString(log.Bytes));
    }

    [TestMethod]
    public void Bytes_BothHalvesGrowBetweenTwoLooks_KeepTheOrderTheHalvesWereGiven()
    {
        var first = new GrowingHalf();
        var second = new GrowingHalf();
        var log = new ArrivalOrderedReceivedLog([first.Snapshot, second.Snapshot]);

        second.Add("b");
        first.Add("a");

        Assert.AreEqual("ab", Encoding.ASCII.GetString(log.Bytes));
    }

    [TestMethod]
    public async Task WrappedConnectionServer_AnotherHalfReceivesBeforeALateWrite_PutsThatHalfFirst()
    {
        var ftp = new GrowingHalf();
        var tftp = new GrowingHalf();
        var log = new ArrivalOrderedReceivedLog([ftp.Snapshot, tftp.Snapshot]);
        var server = log.Wrap(new ScriptedConnectionServer(async connection =>
        {
            tftp.Add("opcode = 1 ");
            await connection.ReadAsync(new byte[1], CancellationToken.None);
            ftp.Add("QUIT");
            await connection.WriteAsync(new byte[] { 1 }, CancellationToken.None);
        }));
        await using var connection = new InMemoryConnection([new byte[] { 1 }]);

        await server.ServeAsync(connection, NewContext());

        Assert.AreEqual("opcode = 1 QUIT", Encoding.ASCII.GetString(log.Bytes));
        CollectionAssert.AreEqual(new byte[] { 1 }, connection.WrittenBytes);
    }

    [TestMethod]
    public async Task WrappedConnectionServer_ForwardsSchemesAndEndPoints()
    {
        var log = new ArrivalOrderedReceivedLog([]);
        EndPointsSeen seen = new();
        var server = log.Wrap(new ScriptedConnectionServer(connection =>
        {
            seen.Local = connection.LocalEndPoint;
            seen.Remote = connection.RemoteEndPoint;
            seen.Peeked = connection.PeekByteAsync(TimeSpan.Zero, CancellationToken.None).AsTask().Result;
            seen.Session = connection.TlsSession;
            connection.Abort();
            return Task.CompletedTask;
        }));
        await using var connection = new InMemoryConnection([new byte[] { 7 }]);

        await server.ServeAsync(connection, NewContext());

        CollectionAssert.AreEqual(new[] { "scripted" }, server.Schemes.ToArray());
        Assert.AreEqual(connection.LocalEndPoint, seen.Local);
        Assert.AreEqual(connection.RemoteEndPoint, seen.Remote);
        Assert.AreEqual((byte)7, seen.Peeked);
        Assert.IsNull(seen.Session);
    }

    [TestMethod]
    public async Task WrappedDatagramServer_AnotherHalfReceivesBeforeALateSend_PutsThatHalfFirst()
    {
        var first = new GrowingHalf();
        var second = new GrowingHalf();
        var log = new ArrivalOrderedReceivedLog([first.Snapshot, second.Snapshot]);
        var server = log.Wrap(new ScriptedDatagramServer(async flow =>
        {
            second.Add("b");
            await flow.SendAsync(new byte[] { 1 }, CancellationToken.None);
            first.Add("a");
            await flow.ReceiveAsync(CancellationToken.None);
        }));
        await using var flow = new InMemoryDatagramFlow(new byte[] { 9 }, [new byte[] { 2 }]);

        await server.ServeAsync(flow, NewContext());

        Assert.AreEqual("ba", Encoding.ASCII.GetString(log.Bytes));
        CollectionAssert.AreEqual(new[] { "scripted" }, server.Schemes.ToArray());
    }

    private static ExchangeContext NewContext() => new(
        1, new ListenUrl("scripted", "127.0.0.1", 0), new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 1),
        new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 2), new RecordingExchangeLog(), TimeProvider.System, CancellationToken.None);

    private sealed class EndPointsSeen
    {
        public System.Net.EndPoint? Local;
        public System.Net.EndPoint? Remote;
        public byte? Peeked;
        public TlsSession? Session;
    }

    private sealed class GrowingHalf
    {
        private readonly object gate = new();
        private byte[] bytes = [];

        public void Add(string text)
        {
            lock (gate)
            {
                bytes = [.. bytes, .. Encoding.ASCII.GetBytes(text)];
            }
        }

        public byte[] Snapshot()
        {
            lock (gate)
            {
                return bytes;
            }
        }
    }

    private sealed class ScriptedConnectionServer(Func<IConnection, Task> serve) : IConnectionProtocolServer
    {
        public IReadOnlyList<string> Schemes { get; } = ["scripted"];

        public Task ServeAsync(IConnection connection, ExchangeContext context) => serve(connection);
    }

    private sealed class ScriptedDatagramServer(Func<IDatagramFlow, Task> serve) : IDatagramProtocolServer
    {
        public IReadOnlyList<string> Schemes { get; } = ["scripted"];

        public Task ServeAsync(IDatagramFlow flow, ExchangeContext context) => serve(flow);
    }
}
