namespace Surl.Protocol.Socks;

/// <summary>
/// The tag and definite length of one BER value (X.690 section 8.1), as the SOCKS library reads it
/// to find the secret of an LDAP bind carried through a tunnel. It may not reference the LDAP server
/// (ADR-0002 decision 3).
/// </summary>
/// <param name="Tag">The identifier octet; every tag an LDAP bind uses takes one.</param>
/// <param name="ValueStart">Where the value starts.</param>
/// <param name="End">Where the length says the value ends, which may be past the bytes held.</param>
internal readonly record struct SocksBerHeader(byte Tag, int ValueStart, long End)
{
    private const byte IndefiniteLength = 0x80;
    private const int MaxLengthOctets = 4;

    /// <summary>Reads the header that starts at <paramref name="start"/>.</summary>
    /// <param name="bytes">The bytes holding it.</param>
    /// <param name="start">Where its identifier octet is.</param>
    /// <param name="header">The header, when <see cref="SocksBerRead.Read"/> is returned.</param>
    /// <returns>Whether the header was read, is cut short, or gives a length that cannot be read.</returns>
    public static SocksBerRead TryRead(ReadOnlySpan<byte> bytes, int start, out SocksBerHeader header)
    {
        header = default;
        if (bytes.Length - start < 2)
        {
            return SocksBerRead.CutShort;
        }

        var first = bytes[start + 1];
        var lengthOctets = first > IndefiniteLength ? first - IndefiniteLength : 0;
        if (first == IndefiniteLength || lengthOctets > MaxLengthOctets)
        {
            return SocksBerRead.Unreadable;
        }

        var valueStart = start + 2 + lengthOctets;
        if (valueStart > bytes.Length)
        {
            return SocksBerRead.CutShort;
        }

        var length = lengthOctets == 0 ? first : LongLength(bytes[(start + 2)..valueStart]);
        header = new SocksBerHeader(bytes[start], valueStart, valueStart + length);
        return SocksBerRead.Read;
    }

    private static long LongLength(ReadOnlySpan<byte> octets)
    {
        long length = 0;
        foreach (var octet in octets)
        {
            length = (length << 8) | octet;
        }

        return length;
    }
}
