using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace Surl.Protocol.Socks;

/// <summary>
/// Reads a SOCKS4 or SOCKS4a request after its version byte: <c>CD</c>, <c>DSTPORT</c>,
/// <c>DSTIP</c>, <c>USERID</c> and its zero, and for SOCKS4a the host name and its zero
/// (ADR-0121 decision 2), each name bounded by <see cref="MaxNameBytes"/> (decision 6).
/// </summary>
internal static class Socks4RequestReader
{
    /// <summary>
    /// The most bytes a user ID or a SOCKS4a host name may hold before its zero (ADR-0121
    /// decision 6): 255, the longest SOCKS5 and RFC 1929 allow by their length bytes.
    /// </summary>
    public const int MaxNameBytes = 255;

    // CD (1), DSTPORT (2) and DSTIP (4).
    private const int FixedFieldBytes = 7;

    /// <summary>
    /// Reads the rest of the request.
    /// </summary>
    /// <param name="reader">curl's connection, positioned after the version byte.</param>
    /// <param name="cancellationToken">Cuts the read off: the handshake timeout or the exchange's token.</param>
    /// <returns>How the read ended, and the request when it was read whole.</returns>
    public static async Task<(Socks4ReadOutcome Outcome, Socks4Request? Request)> ReadAsync(SocksClientReader reader, CancellationToken cancellationToken)
    {
        var fixedFields = new byte[FixedFieldBytes];
        for (var index = 0; index < FixedFieldBytes; index++)
        {
            var next = await reader.ReadByteAsync(cancellationToken);
            if (next < 0)
            {
                return (Socks4ReadOutcome.Truncated, null);
            }

            fixedFields[index] = (byte)next;
        }

        var (userIdOutcome, userId) = await ReadNameAsync(reader, cancellationToken);
        if (userIdOutcome != Socks4ReadOutcome.Read)
        {
            return (userIdOutcome, null);
        }

        return await ReadRestAsync(reader, fixedFields, userId!, cancellationToken);
    }

    /// <summary>
    /// Whether <c>DSTIP</c> asks for SOCKS4a: <c>0.0.0.x</c> with <c>x</c> not zero.
    /// </summary>
    /// <param name="address">The four bytes of <c>DSTIP</c>.</param>
    /// <returns><see langword="true"/> when a host name follows the user ID.</returns>
    public static bool IsSocks4aAddress(ReadOnlySpan<byte> address) =>
        address[..3].IndexOfAnyExcept((byte)0) < 0 && address[3] != 0;

    private static async Task<(Socks4ReadOutcome Outcome, Socks4Request? Request)> ReadRestAsync(
        SocksClientReader reader, byte[] fixedFields, string userId, CancellationToken cancellationToken)
    {
        var command = fixedFields[0];
        var port = BinaryPrimitives.ReadUInt16BigEndian(fixedFields.AsSpan(1, 2));
        var address = fixedFields.AsSpan(3, 4);
        if (!IsSocks4aAddress(address))
        {
            return (Socks4ReadOutcome.Read, new Socks4Request(command, port, new IPAddress(address), userId, null));
        }

        var (hostOutcome, hostName) = await ReadNameAsync(reader, cancellationToken);

        return hostOutcome == Socks4ReadOutcome.Read
            ? (hostOutcome, new Socks4Request(command, port, null, userId, hostName))
            : (hostOutcome, null);
    }

    private static async Task<(Socks4ReadOutcome Outcome, string? Name)> ReadNameAsync(SocksClientReader reader, CancellationToken cancellationToken)
    {
        var name = new List<byte>();
        while (true)
        {
            var next = await reader.ReadByteAsync(cancellationToken);
            if (next <= 0)
            {
                return next < 0
                    ? (Socks4ReadOutcome.Truncated, null)
                    : (Socks4ReadOutcome.Read, Encoding.Latin1.GetString(name.ToArray()));
            }

            if (name.Count == MaxNameBytes)
            {
                return (Socks4ReadOutcome.NameTooLong, null);
            }

            name.Add((byte)next);
        }
    }
}
