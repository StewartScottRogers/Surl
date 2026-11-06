using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>
/// The refusal of a flow past a connection limit (ADR-0082 decision 7), written for the Initials
/// pinned upstream curl builds sent (Fixtures/ClientInitial) and opened with RFC 9001's Initial keys.
/// </summary>
[TestClass]
public sealed class QuicConnectionRefusalTests
{
    private const string WindowsFixture = "static-8.21.0-win.datagrams.bin";

    [TestMethod]
    [DynamicData(nameof(UnitTestFixtures.RecordedBuilds), typeof(UnitTestFixtures))]
    public void Write_RecordedClientInitial_IsOneServerInitialHoldingConnectionCloseConnectionRefused(string fixture)
    {
        var first = UnitTestFixtures.ReadDatagrams(fixture)[0];
        var (destinationConnectionId, sourceConnectionId) = ClientConnectionIds(first);
        using var random = RandomNumberGenerator.Create();

        var refusal = QuicConnectionRefusal.Write(first, random);

        Assert.IsNotNull(refusal);
        var (client, server) = QuicInitialSecrets.DerivePacketProtection(destinationConnectionId);
        using (client)
        using (server)
        {
            var opened = server.Unprotect(refusal, 0, 0);
            Assert.AreEqual(QuicUnprotectStatus.Unprotected, opened.Status);
            Assert.AreEqual(refusal.Length, opened.PacketLength);
            var packet = (QuicLongHeaderPacket)opened.Packet!;
            Assert.AreEqual(QuicPacketType.Initial, packet.Type);
            CollectionAssert.AreEqual(sourceConnectionId, packet.DestinationConnectionId.ToArray());
            Assert.AreEqual(QuicServerConnection.ServerConnectionIdLength, packet.SourceConnectionId.Length);
            Assert.IsTrue(packet.Token.IsEmpty);
            var close = (QuicConnectionCloseFrame)QuicFrameReader.Read(packet.Payload, QuicPacketType.Initial).Single();
            Assert.IsFalse(close.IsApplicationError);
            Assert.AreEqual((ulong)QuicTransportErrorCode.ConnectionRefused, close.ErrorCode);
            Assert.AreEqual(0UL, close.FrameType);
            Assert.IsTrue(close.ReasonPhrase.IsEmpty);
            CollectionAssert.AreEqual(new byte[] { 0x1c, 0x02, 0x00, 0x00 }, packet.Payload.ToArray());
        }
    }

    [TestMethod]
    public void Write_NullRandom_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicConnectionRefusal.Write(new byte[1200], null!));
    }

    [TestMethod]
    public void Write_DatagramShorterThan1200Bytes_IsNull()
    {
        var first = UnitTestFixtures.ReadDatagrams(WindowsFixture)[0];
        using var random = RandomNumberGenerator.Create();

        Assert.IsNull(QuicConnectionRefusal.Write(first.AsMemory(0, QuicServerConnection.MaxDatagramSize - 1), random));
    }

    [TestMethod]
    public void Write_ShortHeaderDatagram_IsNull()
    {
        var datagram = new byte[QuicServerConnection.MaxDatagramSize];
        datagram[0] = 0x40;
        using var random = RandomNumberGenerator.Create();

        Assert.IsNull(QuicConnectionRefusal.Write(datagram, random));
    }

    [TestMethod]
    public void Write_AnotherVersion_IsNull()
    {
        var datagram = UnitTestFixtures.ReadDatagrams(WindowsFixture)[0];
        datagram[4] = 0x02;
        using var random = RandomNumberGenerator.Create();

        Assert.IsNull(QuicConnectionRefusal.Write(datagram, random));
    }

    [TestMethod]
    public void Write_Version1WithTheFixedBitClear_IsNull()
    {
        var datagram = UnitTestFixtures.ReadDatagrams(WindowsFixture)[0];
        datagram[0] &= 0xbf;
        using var random = RandomNumberGenerator.Create();

        Assert.IsNull(QuicConnectionRefusal.Write(datagram, random));
    }

    [TestMethod]
    public void Write_Version1HeaderThatCannotBeRead_IsNull()
    {
        var datagram = UnitTestFixtures.ReadDatagrams(WindowsFixture)[0];
        datagram[5] = 21;
        using var random = RandomNumberGenerator.Create();

        Assert.IsNull(QuicConnectionRefusal.Write(datagram, random));
    }

    [TestMethod]
    public void Write_Version1HandshakePacket_IsNull()
    {
        var datagram = new QuicLongHeaderPacket(
            QuicPacketType.Handshake, new byte[8], new byte[8], ReadOnlyMemory<byte>.Empty, 1, 0, new byte[QuicServerConnection.MaxDatagramSize]).ToBytes();
        using var random = RandomNumberGenerator.Create();

        Assert.IsNull(QuicConnectionRefusal.Write(datagram, random));
    }

    [TestMethod]
    public void Write_InitialWithADestinationConnectionIdShorterThan8Bytes_IsNull()
    {
        var datagram = new QuicLongHeaderPacket(
            QuicPacketType.Initial, new byte[7], new byte[8], ReadOnlyMemory<byte>.Empty, 1, 0, new byte[QuicServerConnection.MaxDatagramSize]).ToBytes();
        using var random = RandomNumberGenerator.Create();

        Assert.IsNull(QuicConnectionRefusal.Write(datagram, random));
    }

    [TestMethod]
    public void Write_InitialThatDoesNotOpenWithTheClientsInitialKeys_IsNull()
    {
        var datagram = UnitTestFixtures.ReadDatagrams(WindowsFixture)[0];
        datagram[100] ^= 0x01;
        using var random = RandomNumberGenerator.Create();

        Assert.IsNull(QuicConnectionRefusal.Write(datagram, random));
    }

    // The connection IDs in the clear part of a long header: the length bytes at offset 5 and
    // after the Destination Connection ID (RFC 9000 section 17.2).
    private static (byte[] Destination, byte[] Source) ClientConnectionIds(byte[] datagram)
    {
        var destinationLength = datagram[5];
        var sourceLength = datagram[6 + destinationLength];
        return (datagram.AsSpan(6, destinationLength).ToArray(), datagram.AsSpan(7 + destinationLength, sourceLength).ToArray());
    }
}
