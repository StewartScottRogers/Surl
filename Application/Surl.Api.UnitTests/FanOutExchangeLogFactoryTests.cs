using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

[TestClass]
public sealed class FanOutExchangeLogFactoryTests
{
    [TestMethod]
    public void Create_EveryCall_ReachesTheFirstFactorysLogThenTheSeconds()
    {
        List<string> calls = [];
        var factory = new FanOutExchangeLogFactory(new CallListingFactory("first", calls), new CallListingFactory("second", calls));

        var log = factory.Create(4, new IPEndPoint(IPAddress.Loopback, 50001));
        log.BytesReceived("ab"u8);
        log.BytesSent("c"u8);
        log.Note("why");
        factory.NoteOutsideExchange("outside");

        CollectionAssert.AreEqual(
            new[]
            {
                "first create 4 127.0.0.1:50001", "second create 4 127.0.0.1:50001",
                "first received 2", "second received 2",
                "first sent 1", "second sent 1",
                "first note why", "second note why",
                "first outside outside", "second outside outside",
            },
            calls);
    }

    private sealed class CallListingFactory(string name, List<string> calls) : IExchangeLogFactory
    {
        public IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint)
        {
            calls.Add($"{name} create {exchangeId} {remoteEndPoint}");
            return new CallListingLog(name, calls);
        }

        public void NoteOutsideExchange(string text) => calls.Add($"{name} outside {text}");
    }

    private sealed class CallListingLog(string name, List<string> calls) : IExchangeLog
    {
        public void BytesReceived(ReadOnlySpan<byte> bytes) => calls.Add($"{name} received {bytes.Length}");

        public void BytesSent(ReadOnlySpan<byte> bytes) => calls.Add($"{name} sent {bytes.Length}");

        public void Note(string text) => calls.Add($"{name} note {text}");
    }
}
