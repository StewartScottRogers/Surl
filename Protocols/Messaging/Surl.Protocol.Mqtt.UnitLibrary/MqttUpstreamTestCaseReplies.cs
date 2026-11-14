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
/// Of upstream's MQTT <c>&lt;servercmd&gt;</c> instructions, which make the server send a
/// deliberately broken or refusing packet, only <c>error-CONNACK</c>, <c>remlen-CONNACK</c> and
/// <c>PINGRESP-as-CONNACK</c> are implemented (ADR-0155); a case with another is skipped
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
        : this(data, [])
    {
    }

    /// <summary>
    /// Holds a case's reply and <c>&lt;servercmd&gt;</c> lines for the MQTT server half (ADR-0155
    /// decision 0).
    /// </summary>
    /// <param name="data">The case's <c>&lt;data&gt;</c>.</param>
    /// <param name="serverCommands">The case's <c>&lt;servercmd&gt;</c> lines.</param>
    /// <exception cref="NotSupportedException">A line names a command <see cref="SupportedServerCommands"/> lacks, or gives a flag a value other than <c>TRUE</c>.</exception>
    /// <exception cref="FormatException">A command's argument is missing or out of range.</exception>
    public MqttUpstreamTestCaseReplies(byte[] data, IReadOnlyList<string> serverCommands)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(serverCommands);

        Data = data;
        foreach (var line in serverCommands)
        {
            ReadServerCommand(line.Trim());
        }
    }

    /// <summary>
    /// The first words of the <c>&lt;servercmd&gt;</c> lines the MQTT server half implements:
    /// <c>Testnum</c>, <c>error-CONNACK</c>, <c>remlen-CONNACK</c> and <c>PINGRESP-as-CONNACK</c>
    /// (ADR-0155 decisions 1 to 3). A case using any other is skipped
    /// <c>servercmd-unsupported:&lt;command&gt;</c>.
    /// </summary>
    public static IReadOnlySet<string> SupportedServerCommands { get; } = new HashSet<string>(
        ["Testnum", "error-CONNACK", "remlen-CONNACK", "PINGRESP-as-CONNACK"], StringComparer.Ordinal);

    /// <summary>
    /// What replaces the <c>CONNACK</c> after the client's <c>CONNECT</c>, then ends the
    /// connection, or <see langword="null"/> when the case gives none.
    /// </summary>
    internal ConnectAnswer? ConnectAnswer { get; private set; }

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

    /// <summary>Logs a packet the half wrote itself, under a name and remaining length of its own.</summary>
    /// <param name="answer">The packet and how it is logged.</param>
    internal void RecordAnswer(ConnectAnswer answer)
    {
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"server {answer.LoggedName} {answer.LoggedRemainingLength:x} {Convert.ToHexStringLower(answer.Bytes)}\n");
        lock (gate)
        {
            received.Write(Encoding.ASCII.GetBytes(line));
        }
    }

    private void ReadServerCommand(string line)
    {
        if (line.Length == 0 || line[0] == '#')
        {
            return;
        }

        var words = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = words[0];
        var argument = words.Length > 1 ? words[1] : string.Empty;
        switch (command)
        {
            case "Testnum":
                break;
            case "error-CONNACK":
                ConnectAnswer = new ConnectAnswer("CONNACK", 2, [0x20, 0x02, 0x00, ReadNumber(command, argument, 1, 5)]);
                break;
            case "remlen-CONNACK":
                var remainingLength = ReadNumber(command, argument, 0, 127);
                ConnectAnswer = new ConnectAnswer("CONNACK", remainingLength, [0x20, remainingLength, 0x00, 0x00]);
                break;
            case "PINGRESP-as-CONNACK":
                ReadFlag(command, argument);
                ConnectAnswer = new ConnectAnswer("PINGRESP-as-CONNACK", 2, [0xD0, 0x02, 0x00, 0x00]);
                break;
            default:
                throw new NotSupportedException($"servercmd-unsupported:{command}");
        }
    }

    private static byte ReadNumber(string command, string argument, int minimum, int maximum) =>
        int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= minimum && number <= maximum
            ? (byte)number
            : throw new FormatException($"{command} needs a number from {minimum} to {maximum}, not '{argument}'.");

    private static void ReadFlag(string command, string argument)
    {
        if (argument != "TRUE")
        {
            throw new NotSupportedException($"servercmd-unsupported:{command} {argument}");
        }
    }

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
