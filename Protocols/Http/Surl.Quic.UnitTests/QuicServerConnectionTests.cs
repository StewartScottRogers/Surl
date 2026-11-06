using System.Security.Cryptography;
using Surl.Tls13;

namespace Surl.Quic;

/// <summary>
/// Drives a <see cref="QuicServerConnection"/> with the Initial datagrams pinned upstream curl
/// builds sent (BL-363's recordings, with the test client's key share put in) and with packets the
/// test client seals, and checks what the server sends with that client. Nothing comes from the
/// Curl port (ADR-0003).
/// </summary>
[TestClass]
public sealed partial class QuicServerConnectionTests
{
    [TestMethod]
    [DynamicData(nameof(UnitTestFixtures.RecordedBuilds), typeof(UnitTestFixtures))]
    public void ReceiveDatagram_RecordedUpstreamCurlInitials_CompletesTheHandshakeWithTheTestClient(string fixture)
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key), fixture);

        exchange.SendRecordedInitials();
        exchange.SendFinished();

        Assert.IsTrue(exchange.Server.IsHandshakeComplete);
        Assert.AreEqual("h3", exchange.Server.ApplicationProtocol);
        Assert.AreEqual((ushort)0x1302, exchange.Client.Tls.CipherSuite);
        Assert.IsTrue(exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicHandshakeDoneFrame>().Any());
        Assert.IsTrue(exchange.Client.FramesReceived[QuicPacketType.Initial].OfType<QuicAckFrame>().Any());
        CollectionAssert.AreEqual(exchange.Server.ServerConnectionId.ToArray(), exchange.Client.ServerConnectionId);
        Assert.AreEqual((ulong)262144, exchange.Server.PeerTransportParameters!.InitialMaxStreamsBidi);
    }

    [TestMethod]
    [DynamicData(nameof(UnitTestFixtures.RecordedBuilds), typeof(UnitTestFixtures))]
    public void ReceiveDatagram_RecordedUpstreamCurlInitials_SendsAdr0082TransportParameters(string fixture)
    {
        using var key = RSA.Create(2048);
        using var exchange = new UnitTestExchange(UnitTestCertificates.Options(key), fixture);

        exchange.SendRecordedInitials();
        var parameters = QuicTransportParameters.Read(exchange.Client.Tls.ServerTransportParameters());

        CollectionAssert.AreEqual(exchange.Client.OriginalDestinationConnectionId, parameters.OriginalDestinationConnectionId!.Value.ToArray());
        CollectionAssert.AreEqual(exchange.Client.ServerConnectionId, parameters.InitialSourceConnectionId!.Value.ToArray());
        Assert.AreEqual((ulong)30000, parameters.MaxIdleTimeout);
        Assert.AreEqual((ulong)1048576, parameters.InitialMaxData);
        Assert.AreEqual((ulong)262144, parameters.InitialMaxStreamDataBidiRemote);
        Assert.AreEqual((ulong)65536, parameters.InitialMaxStreamDataUni);
        Assert.AreEqual((ulong)100, parameters.InitialMaxStreamsBidi);
        Assert.AreEqual((ulong)3, parameters.InitialMaxStreamsUni);
        Assert.IsTrue(parameters.DisableActiveMigration);
        Assert.AreEqual(QuicVersions.Version1, parameters.VersionInformation!.ChosenVersion);
        CollectionAssert.AreEqual(new[] { QuicVersions.Version1 }, parameters.VersionInformation.AvailableVersions.ToArray());
        Assert.IsNull(parameters.InitialMaxStreamDataBidiLocal);
        Assert.IsNull(parameters.StatelessResetToken);
    }

    [TestMethod]
    public void SendApplicationFrames_AfterTheHandshake_SealsOneRttPacketsTheClientOpens()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var output = exchange.Server.SendApplicationFrames([new QuicStreamFrame(3, 0, new byte[] { 1, 2, 3 }, true)]);
        exchange.Client.Receive(output.Datagrams);

        var stream = exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicStreamFrame>().Single();
        Assert.AreEqual((ulong)3, stream.StreamId);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, stream.Data.ToArray());
    }

    [TestMethod]
    public void ReceiveDatagram_OneRttStreamFrame_ReachesItsStreamAndIsAcknowledged()
    {
        using var key = RSA.Create(2048);
        using var exchange = UnitTestExchange.Completed(key);

        var output = exchange.Server.ReceiveDatagram(exchange.Client.SealOneRtt(new QuicStreamFrame(0, 0, new byte[] { 7 }, true), new QuicPingFrame()));
        exchange.Client.Receive(output.Datagrams);

        var stream = output.Streams.Single();
        Assert.AreEqual((ulong)0, stream.Id);
        Assert.AreEqual(1, stream.ReadableLength);
        var ack = exchange.Client.FramesReceived[QuicPacketType.OneRtt].OfType<QuicAckFrame>().Single();
        Assert.AreEqual((ulong)0, ack.LargestAcknowledged);
    }
}
