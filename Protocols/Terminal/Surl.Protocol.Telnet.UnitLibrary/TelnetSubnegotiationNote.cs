using System.Buffers.Binary;
using System.Text;
using static Surl.Protocol.Telnet.TelnetCodes;

namespace Surl.Protocol.Telnet;

/// <summary>
/// Turns a subnegotiation the client sent into the one exchange-log note that reports it: its
/// terminal type (RFC 1091), X display location (RFC 1096), window size (RFC 1073) or
/// environment variables (RFC 1572). Values are decoded as UTF-8.
/// </summary>
internal static class TelnetSubnegotiationNote
{
    private const byte EnvironmentVariable = 0;
    private const byte EnvironmentValue = 1;
    private const byte EnvironmentEscape = 2;
    private const byte EnvironmentInfo = 2;
    private const byte EnvironmentUserVariable = 3;

    private static readonly Dictionary<byte, PayloadDescriber> Describers = new()
    {
        [TerminalTypeOption] = payload => DescribeValue("terminal type (TERMINAL-TYPE)", payload),
        [DisplayLocationOption] = payload => DescribeValue("X display location (X-DISPLAY-LOCATION)", payload),
        [WindowSizeOption] = DescribeWindowSize,
        [NewEnvironmentOption] = DescribeEnvironment,
    };

    private delegate string? PayloadDescriber(ReadOnlySpan<byte> payload);

    /// <summary>
    /// The note that reports <paramref name="payload"/>.
    /// </summary>
    /// <param name="option">The option the subnegotiation names.</param>
    /// <param name="payload">Its bytes after the option.</param>
    /// <returns>The note; a subnegotiation Surl cannot read gets a note saying so.</returns>
    public static string Describe(byte option, ReadOnlySpan<byte> payload) =>
        (Describers.TryGetValue(option, out var describer) ? describer(payload) : null)
        ?? $"The client sent a subnegotiation for option {option} that surl cannot read; ignored.";

    private static string? DescribeValue(string what, ReadOnlySpan<byte> payload) =>
        payload.StartsWith([Is]) ? $"The client's {what} is {Text(payload[1..])}." : null;

    private static string? DescribeWindowSize(ReadOnlySpan<byte> payload) =>
        payload.Length == 4
            ? $"The client's window size (NAWS) is {BinaryPrimitives.ReadUInt16BigEndian(payload)}x{BinaryPrimitives.ReadUInt16BigEndian(payload[2..])}."
            : null;

    private static string? DescribeEnvironment(ReadOnlySpan<byte> payload) =>
        payload.StartsWith([Is]) || payload.StartsWith([EnvironmentInfo])
            ? $"The client's environment (NEW-ENVIRON) is {DescribeVariables(payload[1..])}."
            : null;

    private static string DescribeVariables(ReadOnlySpan<byte> variables)
    {
        var descriptions = new List<string>();
        var start = NextVariableStart(variables, 0);
        while (start < variables.Length)
        {
            var end = NextVariableStart(variables, start + 1);
            descriptions.Add(DescribeVariable(variables[(start + 1)..end]));
            start = end;
        }

        return descriptions.Count == 0 ? "empty" : string.Join(", ", descriptions);
    }

    private static int NextVariableStart(ReadOnlySpan<byte> variables, int from)
    {
        for (var index = from; index < variables.Length; index++)
        {
            if (variables[index] == EnvironmentEscape)
            {
                index++;
            }
            else if (variables[index] is EnvironmentVariable or EnvironmentUserVariable)
            {
                return index;
            }
        }

        return variables.Length;
    }

    private static string DescribeVariable(ReadOnlySpan<byte> variable)
    {
        var name = new List<byte>();
        List<byte>? value = null;
        var target = name;
        for (var index = 0; index < variable.Length; index++)
        {
            if (variable[index] == EnvironmentValue)
            {
                value = [];
                target = value;
                continue;
            }

            index += variable[index] == EnvironmentEscape && index + 1 < variable.Length ? 1 : 0;
            target.Add(variable[index]);
        }

        return value is null
            ? $"{Text(name.ToArray())} (undefined)"
            : $"{Text(name.ToArray())}={Text(value.ToArray())}";
    }

    private static string Text(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);
}
