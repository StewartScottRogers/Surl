namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A message header without its four counts (RFC 1035 section 4.1.1): the writer counts the
/// sections it is given, and the reader keeps the counts to itself.
/// </summary>
/// <param name="Id">The 16-bit identifier a response copies from its query.</param>
/// <param name="IsResponse">QR: the message is a response.</param>
/// <param name="Opcode">OPCODE, 0 to 15.</param>
/// <param name="IsAuthoritativeAnswer">AA.</param>
/// <param name="IsTruncated">TC.</param>
/// <param name="IsRecursionDesired">RD.</param>
/// <param name="IsRecursionAvailable">RA.</param>
/// <param name="Z">The three bits between RA and RCODE, 0 to 7 (Z, with AD and CD of RFC 4035).</param>
/// <param name="ResponseCode">RCODE, 0 to 15.</param>
public sealed record DnsHeader(
    ushort Id,
    bool IsResponse,
    DnsOpcode Opcode,
    bool IsAuthoritativeAnswer,
    bool IsTruncated,
    bool IsRecursionDesired,
    bool IsRecursionAvailable,
    byte Z,
    DnsResponseCode ResponseCode)
{
    /// <summary>The header's size on the wire, counts included.</summary>
    public const int Length = 12;

    /// <summary>Returns the header's second 16-bit word, the flags.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The opcode or the response code is past 15, or <see cref="Z"/> past 7.
    /// </exception>
    internal ushort ToFlags()
    {
        if ((byte)Opcode > 15 || Z > 7 || (byte)ResponseCode > 15)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DnsHeader), "The opcode and the response code hold 4 bits each, and Z 3.");
        }

        return (ushort)(Bit(IsResponse, 15)
            | ((byte)Opcode << 11)
            | Bit(IsAuthoritativeAnswer, 10)
            | Bit(IsTruncated, 9)
            | Bit(IsRecursionDesired, 8)
            | Bit(IsRecursionAvailable, 7)
            | (Z << 4)
            | (byte)ResponseCode);
    }

    /// <summary>
    /// Returns the header whose identifier is <paramref name="id"/> and whose flags word is
    /// <paramref name="flags"/>.
    /// </summary>
    internal static DnsHeader FromFlags(ushort id, ushort flags) => new(
        id,
        IsSet(flags, 15),
        (DnsOpcode)((flags >> 11) & 0xF),
        IsSet(flags, 10),
        IsSet(flags, 9),
        IsSet(flags, 8),
        IsSet(flags, 7),
        (byte)((flags >> 4) & 0x7),
        (DnsResponseCode)(flags & 0xF));

    private static int Bit(bool value, int position) => value ? 1 << position : 0;

    private static bool IsSet(ushort flags, int position) => (flags & (1 << position)) != 0;
}
