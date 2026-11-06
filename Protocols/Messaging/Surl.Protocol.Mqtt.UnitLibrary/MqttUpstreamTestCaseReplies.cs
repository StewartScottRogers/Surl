using System.Globalization;
using System.Text;

namespace Surl.Protocol.Mqtt;

/// <summary>
/// One upstream test case's <c>&lt;reply&gt;</c> as the MQTT server half plays it (ADR-0080
/// decision 2): the <c>&lt;data&gt;</c> a subscriber is sent, and the log of every packet each
/// way.
/// </summary>
/// <remarks>
/// <para>
/// FILEFORMAT.md at tag <c>curl-8_21_0</c> makes <c>&lt;data&gt;</c> the payload published to a
/// subscriber: the server half makes it the retained message of every topic a <c>SUBSCRIBE</c>
/// names, just before Surl's server reads that <c>SUBSCRIBE</c>, so Surl's own answer delivers it.
/// </para>
/// <para>
/// <see cref="ReceivedBytes"/> is the log a case's <c>&lt;protocol&gt;</c> is checked against, one
/// line per whole packet in the order it crossed the connection, as runtests' MQTT server writes
/// it: <c>client</c> or <c>server</c>, the packet type's name, its remaining length in lower-case
/// hexadecimal, then its bytes in lower-case hexadecimal - a client packet's remaining bytes (its
/// whole packet when it has none), a server packet's whole packet.
/// </para>
/// <para>
/// None of upstream's MQTT <c>&lt;servercmd&gt;</c> instructions is implemented (each makes the
/// server send a deliberately broken or refusing packet), so a case with one is skipped
/// <c>servercmd-unsupported:&lt;command&gt;</c>. One instance serves every connection of one case,
/// and is safe to use from several at once. Never compose it into the shipped <c>surl</c>; it is a
/// test seam that <c>Surl.Conformance.UnitTests</c> composes.
/// </para>
/// </remarks>
public sealed class MqttUpstreamTestCaseReplies
{
    private static readonly string[] PacketTypeNames =
    [
        "RESERVED", "CONNECT", "CONNACK", "PUBLISH", "PUBACK", "PUBREC", "PUBREL", "PUBCOMP",
        "SUBSCRIBE", "SUBACK", "UNSUBSCRIBE", "UNSUBACK", "PINGREQ", "PINGRESP", "DISCONNECT", "AUTH",
    ];

    private readonly Lock gate = new();
    private readonly MemoryStream received = new();

    /// <summary>
    /// Holds a case's reply for the MQTT server half.
    /// </summary>
    /// <param name="data">The case's <c>&lt;data&gt;</c>.</param>
    public MqttUpstreamTestCaseReplies(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        Data = data;
    }

    /// <summary>
    /// The first words of the <c>&lt;servercmd&gt;</c> lines the MQTT server half implements: none.
    /// </summary>
    public static IReadOnlySet<string> SupportedServerCommands { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// The packet log, as <see cref="MqttUpstreamTestCaseReplies"/> describes: what the case's
    /// <c>&lt;protocol&gt;</c> is checked against.
    /// </summary>
    public byte[] ReceivedBytes
    {
        get
        {
            lock (gate)
            {
                return received.ToArray();
            }
        }
    }

    /// <summary>The payload every subscribed topic is given.</summary>
    internal byte[] Data { get; }

    /// <summary>Logs one whole packet.</summary>
    /// <param name="fromClient">Whether the client sent it.</param>
    /// <param name="packet">The whole packet, fixed header included.</param>
    /// <param name="headerLength">How many bytes its fixed header takes.</param>
    internal void RecordPacket(bool fromClient, ReadOnlySpan<byte> packet, int headerLength)
    {
        var remaining = packet[headerLength..];
        var shown = fromClient && remaining.Length > 0 ? remaining : packet;
        var direction = fromClient ? "client" : "server";
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{direction} {PacketTypeNames[packet[0] >> 4]} {remaining.Length:x} {Convert.ToHexStringLower(shown)}\n");
        lock (gate)
        {
            received.Write(Encoding.ASCII.GetBytes(line));
        }
    }
}
