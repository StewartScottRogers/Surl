using System.Collections.Frozen;

namespace Surl.Cli;

/// <summary>
/// The default port of each scheme an extended protocol server answers: a scheme the Curl port
/// adds and upstream curl does not have (ADR-0083, decision 7). <see cref="ListenUrlParser"/>
/// consults it only for the schemes a registered extended server claims, and only after
/// <see cref="SchemeDefaultPorts"/>, upstream curl's own table, which this one never shadows.
/// </summary>
/// <remarks>
/// Each port, and where it comes from, row for row as ADR-0083 decision 7 gives it:
/// <list type="bullet">
/// <item><c>s3</c>, <c>az</c>, <c>abfss</c>, <c>gs</c>, <c>oci</c> 443: HTTPS (RFC 9110 section 4.2.2); each service is served over HTTPS.</item>
/// <item><c>abfs</c> 80: HTTP (RFC 9110 section 4.2.1); <c>abfs</c> is the Data Lake scheme without TLS.</item>
/// <item><c>grpc</c>, <c>signalr</c>, <c>socketio</c>, <c>mcp</c> 80: HTTP (RFC 9110 section 4.2.1); each rides HTTP without TLS.</item>
/// <item><c>grpcs</c>, <c>signalrs</c>, <c>socketios</c>, <c>mcps</c> 443: HTTPS (RFC 9110 section 4.2.2).</item>
/// <item><c>webtransport</c> 443: HTTPS over HTTP/3 (draft-ietf-webtrans-http3, RFC 9114).</item>
/// <item><c>coap</c> 5683: RFC 7252 section 6.1, IANA <c>coap</c>.</item>
/// <item><c>coaps</c> 5684: RFC 7252 section 6.2, IANA <c>coaps</c>.</item>
/// <item><c>dns</c> 53: RFC 1035 section 4.2, IANA <c>domain</c>.</item>
/// <item><c>mqtt5</c> 1883: MQTT 5.0 section 4.2, IANA <c>mqtt</c>.</item>
/// <item><c>mqtt5s</c> 8883: MQTT 5.0 section 4.2, IANA <c>secure-mqtt</c>.</item>
/// <item><c>mqtt5+ws</c> 80: WebSocket (RFC 6455 section 3), MQTT 5.0 section 6.</item>
/// <item><c>amqp</c>, <c>amqp1</c> 5672: AMQP 0-9-1 section 4.2.2 and AMQP 1.0 part 2 section 2.1, IANA <c>amqp</c>.</item>
/// <item><c>amqps</c>, <c>amqp1s</c> 5671: IANA <c>amqps</c>.</item>
/// <item><c>kafka</c> 9092: Apache Kafka documentation (default <c>listeners</c>); not IANA-registered.</item>
/// <item><c>kafkas</c> 9093: Apache Kafka documentation's TLS listener examples; not IANA-registered.</item>
/// <item><c>nats</c>, <c>natss</c> 4222: NATS documentation (client port; TLS on the same port); not IANA-registered.</item>
/// <item><c>stomp</c> 61613: the port the STOMP brokers' documentation uses (ActiveMQ, RabbitMQ); STOMP 1.2 fixes none.</item>
/// <item><c>stomps</c> 61614: as <c>stomp</c>, their TLS port.</item>
/// <item><c>xmpp</c> 5222: RFC 6120 section 14.7, IANA <c>xmpp-client</c>.</item>
/// <item><c>xmpps</c> 5223: XEP-0368 (direct TLS), the conventional direct-TLS client port.</item>
/// <item><c>redis</c>, <c>rediss</c> 6379: IANA <c>redis</c>; Redis serves TLS on a port of its own choosing and documents 6379.</item>
/// </list>
/// <c>mcp+stdio</c> has no row: it is served on <c>surl</c>'s own standard input and output, its
/// listen URL names no port (ADR-0111 decision 5), and <see cref="ListenUrlParser"/> reads it apart.
/// </remarks>
public static class ExtendedSchemeDefaultPorts
{
    private const int Http = 80;
    private const int Https = 443;

    private static readonly FrozenDictionary<string, int> DefaultPortByScheme = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["abfs"] = Http,
        ["abfss"] = Https,
        ["amqp"] = 5672,
        ["amqp1"] = 5672,
        ["amqp1s"] = 5671,
        ["amqps"] = 5671,
        ["az"] = Https,
        ["coap"] = 5683,
        ["coaps"] = 5684,
        ["dns"] = 53,
        ["gs"] = Https,
        ["grpc"] = Http,
        ["grpcs"] = Https,
        ["kafka"] = 9092,
        ["kafkas"] = 9093,
        ["mcp"] = Http,
        ["mcps"] = Https,
        ["mqtt5"] = 1883,
        ["mqtt5+ws"] = Http,
        ["mqtt5s"] = 8883,
        ["nats"] = 4222,
        ["natss"] = 4222,
        ["oci"] = Https,
        ["redis"] = 6379,
        ["rediss"] = 6379,
        ["s3"] = Https,
        ["signalr"] = Http,
        ["signalrs"] = Https,
        ["socketio"] = Http,
        ["socketios"] = Https,
        ["stomp"] = 61613,
        ["stomps"] = 61614,
        ["webtransport"] = Https,
        ["xmpp"] = 5222,
        ["xmpps"] = 5223,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Every extended scheme with a default port, lower-case, in no particular order.
    /// </summary>
    public static IReadOnlyCollection<string> Schemes => DefaultPortByScheme.Keys;

    /// <summary>
    /// Looks up the default port of a lower-case extended scheme.
    /// </summary>
    /// <param name="scheme">The scheme, lower-case (<c>s3</c>, not <c>S3</c>).</param>
    /// <param name="defaultPort">The scheme's default port, or 0 when the table has no row for it.</param>
    /// <returns><see langword="true"/> when the table has a row for <paramref name="scheme"/>.</returns>
    public static bool TryGetDefaultPort(string scheme, out int defaultPort) =>
        DefaultPortByScheme.TryGetValue(scheme, out defaultPort);
}
