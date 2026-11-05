using System.Buffers.Binary;

namespace Surl.Quic;

/// <summary>
/// The QUIC transport parameters of RFC 9000 section 18 (and <c>version_information</c>, RFC
/// 9368), carried in TLS's <c>quic_transport_parameters</c> extension. A null property is a
/// parameter that is absent, so its default applies. Parameters this type does not know are
/// skipped when read (RFC 9000 section 18.1).
/// </summary>
public sealed record QuicTransportParameters
{
    /// <summary>The largest <c>initial_max_streams_bidi</c> or <c>initial_max_streams_uni</c> allowed, 2^60.</summary>
    public const ulong MaxStreamCount = 1UL << 60;

    /// <summary>The smallest <c>max_udp_payload_size</c> allowed, 1200 bytes.</summary>
    public const ulong MinUdpPayloadSize = 1200;

    /// <summary>The largest <c>ack_delay_exponent</c> allowed, 20.</summary>
    public const ulong MaxAckDelayExponent = 20;

    /// <summary>The first <c>max_ack_delay</c> not allowed, 2^14 milliseconds.</summary>
    public const ulong MaxAckDelayLimit = 1UL << 14;

    /// <summary>The smallest <c>active_connection_id_limit</c> allowed, 2.</summary>
    public const ulong MinActiveConnectionIdLimit = 2;

    private const int StatelessResetTokenLength = 16;
    private const int MinPreferredAddressLength = 4 + 2 + 16 + 2 + 1 + StatelessResetTokenLength;

    private static readonly Dictionary<ulong, Func<QuicTransportParameters, ReadOnlyMemory<byte>, QuicTransportParameters>> Readers = new()
    {
        [0x00] = (parameters, value) => parameters with { OriginalDestinationConnectionId = ConnectionId(0x00, value) },
        [0x01] = (parameters, value) => parameters with { MaxIdleTimeout = Integer(0x01, value) },
        [0x02] = (parameters, value) => parameters with { StatelessResetToken = FixedLength(0x02, value, StatelessResetTokenLength, StatelessResetTokenLength) },
        [0x03] = (parameters, value) => parameters with { MaxUdpPayloadSize = AtLeast(0x03, value, MinUdpPayloadSize) },
        [0x04] = (parameters, value) => parameters with { InitialMaxData = Integer(0x04, value) },
        [0x05] = (parameters, value) => parameters with { InitialMaxStreamDataBidiLocal = Integer(0x05, value) },
        [0x06] = (parameters, value) => parameters with { InitialMaxStreamDataBidiRemote = Integer(0x06, value) },
        [0x07] = (parameters, value) => parameters with { InitialMaxStreamDataUni = Integer(0x07, value) },
        [0x08] = (parameters, value) => parameters with { InitialMaxStreamsBidi = AtMost(0x08, value, MaxStreamCount) },
        [0x09] = (parameters, value) => parameters with { InitialMaxStreamsUni = AtMost(0x09, value, MaxStreamCount) },
        [0x0a] = (parameters, value) => parameters with { AckDelayExponent = AtMost(0x0a, value, MaxAckDelayExponent) },
        [0x0b] = (parameters, value) => parameters with { MaxAckDelay = AtMost(0x0b, value, MaxAckDelayLimit - 1) },
        [0x0c] = (parameters, value) => parameters with { DisableActiveMigration = FixedLength(0x0c, value, 0, 0).IsEmpty },
        [0x0d] = (parameters, value) => parameters with { PreferredAddress = FixedLength(0x0d, value, MinPreferredAddressLength, int.MaxValue) },
        [0x0e] = (parameters, value) => parameters with { ActiveConnectionIdLimit = AtLeast(0x0e, value, MinActiveConnectionIdLimit) },
        [0x0f] = (parameters, value) => parameters with { InitialSourceConnectionId = ConnectionId(0x0f, value) },
        [0x10] = (parameters, value) => parameters with { RetrySourceConnectionId = ConnectionId(0x10, value) },
        [0x11] = (parameters, value) => parameters with { VersionInformation = Versions(value) },
    };

    /// <summary><c>original_destination_connection_id</c> (0x00): the Destination Connection ID of the client's first Initial; the server's only.</summary>
    public ReadOnlyMemory<byte>? OriginalDestinationConnectionId { get; init; }

    /// <summary><c>max_idle_timeout</c> (0x01), in milliseconds; 0 or absent means none.</summary>
    public ulong? MaxIdleTimeout { get; init; }

    /// <summary><c>stateless_reset_token</c> (0x02), 16 bytes; the server's only.</summary>
    public ReadOnlyMemory<byte>? StatelessResetToken { get; init; }

    /// <summary><c>max_udp_payload_size</c> (0x03), at least 1200; 65527 when absent.</summary>
    public ulong? MaxUdpPayloadSize { get; init; }

    /// <summary><c>initial_max_data</c> (0x04); 0 when absent.</summary>
    public ulong? InitialMaxData { get; init; }

    /// <summary><c>initial_max_stream_data_bidi_local</c> (0x05); 0 when absent.</summary>
    public ulong? InitialMaxStreamDataBidiLocal { get; init; }

    /// <summary><c>initial_max_stream_data_bidi_remote</c> (0x06); 0 when absent.</summary>
    public ulong? InitialMaxStreamDataBidiRemote { get; init; }

    /// <summary><c>initial_max_stream_data_uni</c> (0x07); 0 when absent.</summary>
    public ulong? InitialMaxStreamDataUni { get; init; }

    /// <summary><c>initial_max_streams_bidi</c> (0x08), at most 2^60; 0 when absent.</summary>
    public ulong? InitialMaxStreamsBidi { get; init; }

    /// <summary><c>initial_max_streams_uni</c> (0x09), at most 2^60; 0 when absent.</summary>
    public ulong? InitialMaxStreamsUni { get; init; }

    /// <summary><c>ack_delay_exponent</c> (0x0a), at most 20; 3 when absent.</summary>
    public ulong? AckDelayExponent { get; init; }

    /// <summary><c>max_ack_delay</c> (0x0b), in milliseconds, below 2^14; 25 when absent.</summary>
    public ulong? MaxAckDelay { get; init; }

    /// <summary><c>disable_active_migration</c> (0x0c): whether the parameter, which has no value, is present.</summary>
    public bool DisableActiveMigration { get; init; }

    /// <summary><c>preferred_address</c> (0x0d), kept as its bytes; the server's only.</summary>
    public ReadOnlyMemory<byte>? PreferredAddress { get; init; }

    /// <summary><c>active_connection_id_limit</c> (0x0e), at least 2; 2 when absent.</summary>
    public ulong? ActiveConnectionIdLimit { get; init; }

    /// <summary><c>initial_source_connection_id</c> (0x0f): the Source Connection ID of the sender's first Initial.</summary>
    public ReadOnlyMemory<byte>? InitialSourceConnectionId { get; init; }

    /// <summary><c>retry_source_connection_id</c> (0x10): the Source Connection ID of the server's Retry; the server's only.</summary>
    public ReadOnlyMemory<byte>? RetrySourceConnectionId { get; init; }

    /// <summary><c>version_information</c> (0x11, RFC 9368).</summary>
    public QuicVersionInformation? VersionInformation { get; init; }

    /// <summary>Reads the parameters of a <c>quic_transport_parameters</c> extension.</summary>
    /// <param name="encoded">The extension's value.</param>
    /// <returns>The parameters it carries.</returns>
    /// <exception cref="QuicTransportException">
    /// <c>TRANSPORT_PARAMETER_ERROR</c>: the encoding ends early, a parameter appears twice, or a
    /// value is malformed or outside the range RFC 9000 section 18.2 allows.
    /// </exception>
    public static QuicTransportParameters Read(ReadOnlyMemory<byte> encoded)
    {
        var reader = new QuicReader(encoded, QuicTransportErrorCode.TransportParameterError);
        var seen = new HashSet<ulong>();
        var parameters = new QuicTransportParameters();
        while (!reader.IsAtEnd)
        {
            var id = reader.ReadVariableLengthInteger();
            var value = reader.ReadLengthPrefixedBytes();
            if (!seen.Add(id))
            {
                throw Fault($"Transport parameter 0x{id:x} appears more than once.");
            }

            parameters = Readers.TryGetValue(id, out var read) ? read(parameters, value) : parameters;
        }

        return parameters;
    }

    /// <summary>Writes the parameters that are present, in ascending order of their IDs.</summary>
    /// <returns>The value of a <c>quic_transport_parameters</c> extension.</returns>
    /// <exception cref="ArgumentException">A connection ID is longer than 20 bytes.</exception>
    public byte[] ToBytes()
    {
        var writer = new QuicWriter();
        WriteBytes(writer, 0x00, ConnectionIdToWrite(OriginalDestinationConnectionId, nameof(OriginalDestinationConnectionId)));
        WriteInteger(writer, 0x01, MaxIdleTimeout);
        WriteBytes(writer, 0x02, StatelessResetToken);
        WriteInteger(writer, 0x03, MaxUdpPayloadSize);
        WriteInteger(writer, 0x04, InitialMaxData);
        WriteInteger(writer, 0x05, InitialMaxStreamDataBidiLocal);
        WriteInteger(writer, 0x06, InitialMaxStreamDataBidiRemote);
        WriteInteger(writer, 0x07, InitialMaxStreamDataUni);
        WriteInteger(writer, 0x08, InitialMaxStreamsBidi);
        WriteInteger(writer, 0x09, InitialMaxStreamsUni);
        WriteInteger(writer, 0x0a, AckDelayExponent);
        WriteInteger(writer, 0x0b, MaxAckDelay);
        WriteBytes(writer, 0x0c, DisableActiveMigration ? ReadOnlyMemory<byte>.Empty : (ReadOnlyMemory<byte>?)null);
        WriteBytes(writer, 0x0d, PreferredAddress);
        WriteInteger(writer, 0x0e, ActiveConnectionIdLimit);
        WriteBytes(writer, 0x0f, ConnectionIdToWrite(InitialSourceConnectionId, nameof(InitialSourceConnectionId)));
        WriteBytes(writer, 0x10, ConnectionIdToWrite(RetrySourceConnectionId, nameof(RetrySourceConnectionId)));
        WriteBytes(writer, 0x11, VersionInformation is null ? (ReadOnlyMemory<byte>?)null : VersionsToWrite(VersionInformation));
        return writer.ToArray();
    }

    private static QuicTransportException Fault(string message) =>
        new(QuicTransportErrorCode.TransportParameterError, message);

    private static ulong Integer(ulong id, ReadOnlyMemory<byte> value) =>
        QuicVariableLengthInteger.TryRead(value.Span, out var integer, out var length) && length == value.Length
            ? integer
            : throw Fault($"Transport parameter 0x{id:x} is not one variable-length integer.");

    private static ulong AtMost(ulong id, ReadOnlyMemory<byte> value, ulong maximum)
    {
        var integer = Integer(id, value);
        return integer > maximum ? throw Fault($"Transport parameter 0x{id:x} is {integer}; the most allowed is {maximum}.") : integer;
    }

    private static ulong AtLeast(ulong id, ReadOnlyMemory<byte> value, ulong minimum)
    {
        var integer = Integer(id, value);
        return integer < minimum ? throw Fault($"Transport parameter 0x{id:x} is {integer}; the least allowed is {minimum}.") : integer;
    }

    private static ReadOnlyMemory<byte> FixedLength(ulong id, ReadOnlyMemory<byte> value, int minimum, int maximum) =>
        value.Length < minimum || value.Length > maximum
            ? throw Fault($"Transport parameter 0x{id:x} is {value.Length} bytes long.")
            : value;

    private static ReadOnlyMemory<byte> ConnectionId(ulong id, ReadOnlyMemory<byte> value) =>
        FixedLength(id, value, 0, QuicConnectionIds.MaxLength);

    private static QuicVersionInformation Versions(ReadOnlyMemory<byte> value)
    {
        if (value.Length < 4 || value.Length % 4 != 0)
        {
            throw Fault($"version_information is {value.Length} bytes long, not a whole number of versions.");
        }

        var versions = new uint[value.Length / 4];
        for (var index = 0; index < versions.Length; index++)
        {
            versions[index] = BinaryPrimitives.ReadUInt32BigEndian(value.Span[(index * 4)..]);
        }

        return new QuicVersionInformation(versions[0], versions[1..]);
    }

    private static ReadOnlyMemory<byte>? ConnectionIdToWrite(ReadOnlyMemory<byte>? connectionId, string name)
    {
        if (connectionId is { } present)
        {
            QuicConnectionIds.RequireValidLength(present, name);
        }

        return connectionId;
    }

    private static byte[] VersionsToWrite(QuicVersionInformation information)
    {
        var writer = new QuicWriter();
        writer.WriteUInt32(information.ChosenVersion);
        foreach (var version in information.AvailableVersions)
        {
            writer.WriteUInt32(version);
        }

        return writer.ToArray();
    }

    private static void WriteInteger(QuicWriter writer, ulong id, ulong? value)
    {
        if (value is { } integer)
        {
            WriteBytes(writer, id, QuicVariableLengthInteger.Encode(integer));
        }
    }

    private static void WriteBytes(QuicWriter writer, ulong id, ReadOnlyMemory<byte>? value)
    {
        if (value is { } bytes)
        {
            writer.WriteVariableLengthInteger(id);
            writer.WriteLengthPrefixedBytes(bytes.Span);
        }
    }
}
