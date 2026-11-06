using System.Text;
using Surl.MessageBus;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The header block HPUB and HMSG carry (the headers ADR; ADR-0113 decision 4): <c>NATS/1.0</c>,
/// an optional status code and description, then <c>Name: value</c> lines, then an empty line,
/// every line ending CR LF.
/// </summary>
internal static class NatsHeaderBlock
{
    /// <summary>The native form's format: the header block, kept byte for byte (BL-706).</summary>
    public const string NativeFormat = "nats";

    private const string Version = "NATS/1.0";
    private const string LineEnd = "\r\n";

    /// <summary>The 16-byte block of the <c>503</c> no-responders status message.</summary>
    public static ReadOnlyMemory<byte> NoResponders { get; } = "NATS/1.0 503\r\n\r\n"u8.ToArray();

    /// <summary>
    /// The block's <c>Name: value</c> lines as the bus's string headers, names case-preserved and
    /// values trimmed; none when the block does not parse.
    /// </summary>
    public static IReadOnlyList<BusMessageHeader> Parse(ReadOnlySpan<byte> block)
    {
        var text = Encoding.UTF8.GetString(block);
        if (!text.EndsWith(LineEnd + LineEnd, StringComparison.Ordinal))
        {
            return [];
        }

        var lines = text[..^4].Split(LineEnd);
        if (lines[0] != Version && !lines[0].StartsWith(Version + " ", StringComparison.Ordinal))
        {
            return [];
        }

        var headers = new List<BusMessageHeader>();
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                return [];
            }

            headers.Add(new BusMessageHeader(line[..colon], line[(colon + 1)..].Trim()));
        }

        return headers;
    }

    /// <summary>
    /// The block for headers from another protocol: <c>NATS/1.0</c>, one <c>name: value</c> line
    /// per header in order, and the empty line. A header the block cannot carry - an empty name, a
    /// name holding <c>:</c>, or a CR or LF anywhere - is left out; null when none is left.
    /// </summary>
    public static byte[]? Encode(IReadOnlyList<BusMessageHeader> headers)
    {
        var carried = headers.Where(IsCarried).ToList();
        if (carried.Count == 0)
        {
            return null;
        }

        var block = new StringBuilder(Version).Append(LineEnd);
        foreach (var header in carried)
        {
            block.Append(header.Name).Append(": ").Append(header.Value).Append(LineEnd);
        }

        return Encoding.UTF8.GetBytes(block.Append(LineEnd).ToString());
    }

    private static bool IsCarried(BusMessageHeader header) =>
        header.Name.Length > 0
        && header.Name.IndexOfAny([':', '\r', '\n']) < 0
        && header.Value.IndexOfAny(['\r', '\n']) < 0;
}
