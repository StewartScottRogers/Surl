using Surl.MessageBus;
using static Surl.Extended.Protocol.Mqtt5.UnitTests.Mqtt5BrokerExchange;

namespace Surl.Extended.Protocol.Mqtt5.UnitTests;

/// <summary>
/// The one-shot read (BL-976; ADR-0093 decision 9): with the option on, SUBACK and the retained messages are
/// followed by <c>DISCONNECT</c> <c>0x00</c> and a close, and the client's Will is discarded.
/// </summary>
public sealed partial class Mqtt5BrokerTests
{
    private static readonly byte[] RetainedHi = [0x31, 0x06, 0x00, 0x01, 0x74, 0x00, 0x68, 0x69];

    [TestMethod]
    public void DisconnectsAfterRetained_ByDefault_IsOff() =>
        Assert.IsFalse(Broker().DisconnectsAfterRetained);

    [TestMethod]
    public async Task ServeAsync_DisconnectAfterRetained_SendsSubackTheRetainedMessageThenDisconnect0x00AndDiscardsTheWill()
    {
        var state = NewState();
        var published = PublishedOn(state);
        state.Bus.Publish(new BusMessage("t", "hi"u8.ToArray()) { Retain = true });
        published.Clear();
        var clock = new ManualTimeProvider();
        var client = await Mqtt5TestClient.ConnectAsync(
            Broker(state, disconnectsAfterRetained: true),
            Context(clock, TestContext.CancellationToken),
            Connect("s", will: Will()));

        client.Send(Subscribe(1, ("t", 0)));

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, await client.ReadPacketAsync());
        CollectionAssert.AreEqual(RetainedHi, await client.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0xE0, 0x00 }, await client.ReadPacketAsync());
        Assert.IsEmpty(await client.ServingEndedAsync());
        Assert.IsEmpty(published);
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectAfterRetainedWithNothingRetained_SendsSubackThenDisconnect0x00()
    {
        var client = await ConnectClientAsync(Broker(disconnectsAfterRetained: true), "s");

        client.Send(Subscribe(1, ("t", 0)));

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, await client.ReadPacketAsync());
        CollectionAssert.AreEqual(new byte[] { 0xE0, 0x00 }, await client.ReadPacketAsync());
        Assert.IsEmpty(await client.ServingEndedAsync());
    }

    [TestMethod]
    public async Task ServeAsync_DisconnectAfterRetainedOff_KeepsTheConnectionOpenAfterTheRetainedMessage()
    {
        var state = NewState();
        state.Bus.Publish(new BusMessage("t", "hi"u8.ToArray()) { Retain = true });
        var client = await ConnectClientAsync(Broker(state), "s");

        client.Send(Subscribe(1, ("t", 0)));

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x04, 0x00, 0x01, 0x00, 0x00 }, await client.ReadPacketAsync());
        CollectionAssert.AreEqual(RetainedHi, await client.ReadPacketAsync());
        Assert.IsEmpty(await client.DisconnectAsync());
    }
}
