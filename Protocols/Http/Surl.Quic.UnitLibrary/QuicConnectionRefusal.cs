using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Surl.Quic;

/// <summary>
/// The server's answer to a flow it will not serve because it is past a connection limit
/// (ADR-0082 decision 7, "Too many connections"): one Initial packet, protected with the Initial
/// keys of the client's Destination Connection ID, holding <c>CONNECTION_CLOSE</c> (0x1c)
/// <c>CONNECTION_REFUSED</c> (0x02) and no reason phrase (RFC 9000 sections 10.2.3 and 20.1).
/// </summary>
/// <remarks>
/// Only a first datagram a <see cref="QuicServerConnection"/> would start a connection on is
/// answered: a version 1 Initial in a datagram of at least
/// <see cref="QuicServerConnection.MaxDatagramSize"/> bytes, whose Destination Connection ID is at
/// least <see cref="QuicServerConnection.MinClientDestinationConnectionIdLength"/> bytes and whose
/// packet opens with the client's Initial keys. Anything else gets no bytes, so the refusal never
/// answers a datagram the server would have dropped.
/// </remarks>
public static class QuicConnectionRefusal
{
    /// <summary>
    /// Writes the datagram refusing the connection <paramref name="firstDatagram"/> starts, or
    /// returns null when it starts none.
    /// </summary>
    /// <param name="firstDatagram">The client's first datagram on the flow.</param>
    /// <param name="random">The source of the server's Source Connection ID.</param>
    /// <returns>The one datagram to send, or null to send nothing.</returns>
    public static byte[]? Write(ReadOnlyMemory<byte> firstDatagram, RandomNumberGenerator random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (!IsLongHeaderOfStartingSize(firstDatagram.Span)
            || BinaryPrimitives.ReadUInt32BigEndian(firstDatagram.Span[1..]) != QuicVersions.Version1)
        {
            return null;
        }

        try
        {
            return WriteForVersion1(firstDatagram, random);
        }
        catch (QuicTransportException)
        {
            // A header that cannot be read starts nothing, so it is refused with no bytes.
            return null;
        }
    }

    private static bool IsLongHeaderOfStartingSize(ReadOnlySpan<byte> datagram) =>
        datagram.Length >= QuicServerConnection.MaxDatagramSize && (datagram[0] & QuicPacketHeaderBits.HeaderForm) != 0;

    private static byte[]? WriteForVersion1(ReadOnlyMemory<byte> firstDatagram, RandomNumberGenerator random)
    {
        // The reserved and packet number length bits are under header protection, so the header is
        // read only as far as the Destination Connection ID before the packet is opened.
        var reader = new QuicReader(firstDatagram, QuicTransportErrorCode.ProtocolViolation);
        var firstByte = reader.ReadByte();
        reader.ReadUInt32();
        var destination = QuicPacketReader.ReadConnectionId(reader, QuicVersions.Version1);
        var isInitial = (firstByte & (QuicPacketHeaderBits.FixedBit | QuicPacketHeaderBits.LongPacketTypeMask)) == QuicPacketHeaderBits.FixedBit;
        if (!isInitial || destination.Length < QuicServerConnection.MinClientDestinationConnectionIdLength)
        {
            return null;
        }

        var (client, server) = QuicInitialSecrets.DerivePacketProtection(destination.Span);
        using (client)
        using (server)
        {
            var opened = client.Unprotect(firstDatagram, 0, 0);
            if (opened.Status != QuicUnprotectStatus.Unprotected)
            {
                return null;
            }

            var initial = (QuicLongHeaderPacket)opened.Packet!;
            var serverConnectionId = new byte[QuicServerConnection.ServerConnectionIdLength];
            random.GetBytes(serverConnectionId);
            var payload = QuicFrameWriter.Write(
                [new QuicConnectionCloseFrame(false, (ulong)QuicTransportErrorCode.ConnectionRefused, 0, ReadOnlyMemory<byte>.Empty)]);
            var packet = new QuicLongHeaderPacket(
                QuicPacketType.Initial, initial.SourceConnectionId, serverConnectionId, ReadOnlyMemory<byte>.Empty, 1, 0, payload);
            return server.Protect(packet, 0);
        }
    }
}
