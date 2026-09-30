namespace Surl.Protocol.Imap;

/// <summary>
/// The literal announced at the end of a command line: <c>{n}</c>, synchronizing (RFC 3501,
/// section 4.3), or <c>{n+}</c>, non-synchronizing (RFC 7888).
/// </summary>
/// <param name="Start">The index of its <c>{</c> in the line.</param>
/// <param name="Length">The number of bytes announced; <see cref="long.MaxValue"/> when the
/// digits name more than a <see cref="long"/> holds.</param>
/// <param name="IsNonSynchronizing">Whether it is <c>{n+}</c>.</param>
internal sealed record ImapLiteralMarker(int Start, long Length, bool IsNonSynchronizing)
{
    /// <summary>
    /// Finds the literal announced at the end of <paramref name="line"/>.
    /// </summary>
    /// <param name="line">A command line without its CRLF.</param>
    /// <returns>The literal, or <see langword="null"/> when the line does not end with one.</returns>
    public static ImapLiteralMarker? Find(byte[] line)
    {
        if (!line.AsSpan().EndsWith("}"u8))
        {
            return null;
        }

        var isNonSynchronizing = line.AsSpan().EndsWith("+}"u8);
        var digitsEnd = line.Length - (isNonSynchronizing ? 2 : 1);
        var start = line.AsSpan(0, digitsEnd).LastIndexOf((byte)'{');
        return start < 0 ? null : Read(line.AsSpan(start + 1, digitsEnd - start - 1), start, isNonSynchronizing);
    }

    private static ImapLiteralMarker? Read(ReadOnlySpan<byte> digits, int start, bool isNonSynchronizing) =>
        digits.Length == 0 || digits.ContainsAnyExceptInRange((byte)'0', (byte)'9')
            ? null
            : new ImapLiteralMarker(start, long.TryParse(digits, out var length) ? length : long.MaxValue, isNonSynchronizing);
}
