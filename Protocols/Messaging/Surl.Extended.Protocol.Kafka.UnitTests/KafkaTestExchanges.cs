using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka.UnitTests;

/// <summary>
/// Request and response bytes as the Kafka protocol guide lays them out, and exchange contexts,
/// for the broker's tests.
/// </summary>
internal static class KafkaTestExchanges
{
    /// <summary>The correlation id every request built here carries: 7.</summary>
    public const int CorrelationId = 7;

    /// <summary>A size prefix, request header v1 (api_key, api_version, correlation_id 7,
    /// client_id "c"), then <paramref name="fields"/>.</summary>
    public static byte[] Request(KafkaApiKey apiKey, short version, params byte[] fields)
    {
        byte[] header = [.. Int16((short)apiKey), .. Int16(version), 0, 0, 0, CorrelationId, 0x00, 0x01, 0x63];
        return [.. Int32(header.Length + fields.Length), .. header, .. fields];
    }

    /// <summary>A size prefix, response header v0 (correlation_id 7), then <paramref name="body"/>.</summary>
    public static byte[] Response(params byte[] body) => [.. Int32(4 + body.Length), 0, 0, 0, CorrelationId, .. body];

    /// <summary>A STRING: INT16 length and the UTF-8 bytes.</summary>
    public static byte[] String(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return [.. Int16((short)bytes.Length), .. bytes];
    }

    /// <summary>BYTES: INT32 length and the bytes.</summary>
    public static byte[] Bytes(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return [.. Int32(bytes.Length), .. bytes];
    }

    public static byte[] Int16(short value) => [(byte)(value >> 8), (byte)value];

    public static byte[] Int32(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    /// <summary>
    /// Serves <paramref name="requests"/> on one connection with every BL-518 answerer, and
    /// returns what the broker wrote and the exchange log.
    /// </summary>
    public static async Task<(byte[] Written, RecordingExchangeLog Log)> ServeAsync(
        UnitTestKafkaAuthenticationPolicy policy,
        KafkaMetadataAnswerer metadata,
        CancellationToken cancellationToken,
        params byte[][] requests)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection(requests.Select(request => (ReadOnlyMemory<byte>)request));
        var broker = new KafkaBroker(
            policy,
            [new KafkaApiVersionsAnswerer(), metadata, new KafkaSaslHandshakeAnswerer(policy), new KafkaSaslAuthenticateAnswerer()]);

        await broker.ServeAsync(connection, Context(cancellationToken, log));

        return (connection.WrittenBytes, log);
    }

    /// <summary>A Metadata answerer over a new, empty bus.</summary>
    public static KafkaMetadataAnswerer EmptyBusMetadata() => new(new Surl.MessageBus.MessageBus(new ManualTimeProvider()));

    public static ExchangeContext Context(
        CancellationToken cancellationToken,
        IExchangeLog? log = null,
        ListenUrl? listenUrl = null,
        EndPoint? localEndPoint = null) => new(
            1,
            listenUrl ?? new ListenUrl("kafka", "127.0.0.1", 9092).WithBoundPort(9092),
            localEndPoint ?? new IPEndPoint(IPAddress.Loopback, 9092),
            new IPEndPoint(IPAddress.Loopback, 50000),
            log ?? new RecordingExchangeLog(),
            new ManualTimeProvider(),
            cancellationToken);
}
