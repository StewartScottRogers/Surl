using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapProtocolServer.WriteRefusalAsync"/> (ADR-0097 decision 7; RFC 7252 section 5.9.3.4):
/// a flow past a connection limit is answered in CoAP's words, from bytes written by hand.
/// </summary>
[TestClass]
public sealed class CoapProtocolServerRefusalTests
{
    private static readonly CoapAccessRules Anonymous = new(AllowsAnonymous: true, HasAccounts: false);

    [TestMethod]
    public async Task WriteRefusalAsync_ConRequest_IsAnAckWith503AndMaxAge5()
    {
        var sent = await RefuseAsync([0x41, 0x01, 0xBE, 0xEF, 0xC7]);

        CollectionAssert.AreEqual(new byte[] { 0x61, 0xA3, 0xBE, 0xEF, 0xC7, 0xD1, 0x01, 0x05 }, sent.Single());
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NonRequest_IsANon503WithMaxAge5()
    {
        var sent = await RefuseAsync([0x52, 0x01, 0xBE, 0xEF, 0xC7, 0xC8]);

        // The test random source draws Message ID 0xFFFF.
        CollectionAssert.AreEqual(new byte[] { 0x52, 0xA3, 0xFF, 0xFF, 0xC7, 0xC8, 0xD1, 0x01, 0x05 }, sent.Single());
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00 })]
    [DataRow(new byte[] { 0x40, 0x00, 0x00, 0x01 })]
    [DataRow(new byte[] { 0x40, 0x01, 0xBE, 0xEF, 0xFF })]
    [DataRow(new byte[] { 0x41, 0x45, 0x00, 0x01, 0xC0 })]
    [DataRow(new byte[] { 0x61, 0x01, 0x00, 0x01, 0xC0 })]
    [DataRow(new byte[] { 0x71, 0x01, 0x00, 0x01, 0xC0 })]
    public async Task WriteRefusalAsync_DatagramThatIsNoRequest_SendsNothing(byte[] first)
    {
        var sent = await RefuseAsync(first);

        Assert.IsEmpty(sent);
    }

    [TestMethod]
    public async Task WriteRefusalAsync_NullFlow_Throws()
    {
        var server = new CoapProtocolServer(new CoapTestStore().Store, Anonymous);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await server.WriteRefusalAsync(null!, ConnectionRefusal.TooManyConnections, CancellationToken.None));
    }

    private static async Task<IReadOnlyList<byte[]>> RefuseAsync(byte[] first)
    {
        var server = new CoapProtocolServer(new CoapTestStore().Store, Anonymous, new UnitTestCoapRandomSource());
        await using var flow = new InMemoryDatagramFlow(first);

        await server.WriteRefusalAsync(flow, ConnectionRefusal.TooManyConnectionsFromAddress, CancellationToken.None);

        return flow.SentDatagrams;
    }
}
