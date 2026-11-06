using Surl.Dtls;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// <see cref="CoapDtlsFlow"/>'s parts the <c>coaps</c> exchanges in
/// <see cref="SecureCoapProtocolServerTests"/> do not reach: its endpoints and the port move it refuses.
/// </summary>
[TestClass]
public sealed class CoapDtlsFlowTests
{
    [TestMethod]
    public async Task Endpoints_AreTheTransports_AndMoveToNewLocalPort_IsRefused()
    {
        var transport = new UnitTestDtlsDatagramFlow(new UnitTestDtlsClient());
        using var session = new DtlsServerSession(
            transport,
            new DtlsServerOptions(UnitTestCertificates.EcdsaP256, new DtlsCookieSecrets(TimeProvider.System), TimeProvider.System, TimeSpan.FromSeconds(30), 1 << 20));
        using var associationEnded = new CancellationTokenSource();
        await using var flow = new CoapDtlsFlow(transport, session, [0x40, 0x00, 0x00, 0x01], associationEnded);

        Assert.AreSame(transport.LocalEndPoint, flow.LocalEndPoint);
        Assert.AreSame(transport.RemoteEndPoint, flow.RemoteEndPoint);
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x00, 0x00, 0x01 }, flow.FirstDatagram.ToArray());
        Assert.ThrowsExactly<NotSupportedException>(() => flow.MoveToNewLocalPortAsync(CancellationToken.None));
    }
}
