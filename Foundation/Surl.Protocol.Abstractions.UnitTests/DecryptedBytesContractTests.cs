using System.Net;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class DecryptedBytesContractTests
{
    private sealed class RecordingSink : IDecryptedBytesSink
    {
        public List<string> Calls { get; } = [];

        public void Received(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes) =>
            Calls.Add($"R {layer} {streamId} {bytes.Length}");

        public void Sent(ExchangeBytesLayer layer, long? streamId, ReadOnlySpan<byte> bytes) =>
            Calls.Add($"S {layer} {streamId} {bytes.Length}");
    }

    [TestMethod]
    public void Layer_ValuesAreWireDecryptedDecodedFromZero()
    {
        Assert.AreEqual(0, (int)ExchangeBytesLayer.Wire);
        Assert.AreEqual(1, (int)ExchangeBytesLayer.Decrypted);
        Assert.AreEqual(2, (int)ExchangeBytesLayer.Decoded);
    }

    [TestMethod]
    public void ExchangeBytesEvent_BuiltTheOldWay_HasWireLayerAndNoStream()
    {
        var bytesEvent = new ExchangeBytesEvent(1, ExchangeDirection.Received, new byte[] { 1 });

        Assert.AreEqual(ExchangeBytesLayer.Wire, bytesEvent.Layer);
        Assert.IsNull(bytesEvent.StreamId);
    }

    [TestMethod]
    public void ExchangeBytesEvent_LayerAndStreamIdCanBeSet()
    {
        var bytesEvent = new ExchangeBytesEvent(1, ExchangeDirection.Sent, new byte[] { 1 })
        {
            Layer = ExchangeBytesLayer.Decoded,
            StreamId = 4,
        };

        Assert.AreEqual(ExchangeBytesLayer.Decoded, bytesEvent.Layer);
        Assert.AreEqual(4L, bytesEvent.StreamId);
    }

    [TestMethod]
    public void DiscardingSink_AcceptsBytesAndIsOneInstance()
    {
        IDecryptedBytesSink sink = DiscardingDecryptedBytesSink.Instance;

        sink.Received(ExchangeBytesLayer.Decrypted, 0, new byte[] { 1 });
        sink.Sent(ExchangeBytesLayer.Decrypted, null, new byte[] { 1 });

        Assert.AreSame(DiscardingDecryptedBytesSink.Instance, sink);
    }

    [TestMethod]
    public void ReceivedFramePayload_ASinkThatDoesNotImplementIt_TakesTheBytesAsReceived()
    {
        var sink = new RecordingSink();

        ((IDecryptedBytesSink)sink).ReceivedFramePayload(ExchangeBytesLayer.Decrypted, 4, new byte[3]);

        Assert.AreEqual("R Decrypted 4 3", sink.Calls.Single());
    }

    [TestMethod]
    public void ExchangeContext_DecryptedBytes_DefaultsToDiscardingAndCarriesAGivenSink()
    {
        var context = new ExchangeContext(
            1,
            new ListenUrl("https", "localhost", 0),
            new IPEndPoint(IPAddress.Loopback, 1),
            new IPEndPoint(IPAddress.Loopback, 2),
            new RecordingExchangeLog(),
            TimeProvider.System,
            CancellationToken.None);
        var sink = new RecordingSink();

        Assert.AreSame(DiscardingDecryptedBytesSink.Instance, context.DecryptedBytes);

        (context with { DecryptedBytes = sink }).DecryptedBytes.Received(ExchangeBytesLayer.Decrypted, 8, new byte[2]);
        Assert.AreEqual("R Decrypted 8 2", sink.Calls.Single());
    }
}
