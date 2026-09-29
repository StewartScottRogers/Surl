using System.Globalization;

namespace Surl.Protocol.Tftp;

/// <summary>
/// What a read transfer runs with once the client's options are answered (RFC 2347 to
/// RFC 2349): the block size, the retransmission timeout, and the OACK to send first, if any.
/// </summary>
/// <param name="BlockSize">The bytes in every DATA packet but the last.</param>
/// <param name="RetransmissionTimeout">How long the server waits for an ACK before it sends its packet again.</param>
/// <param name="OptionAcknowledgement">The OACK, or <see langword="null"/> when no option was accepted.</param>
internal sealed record TftpNegotiation(int BlockSize, TimeSpan RetransmissionTimeout, byte[]? OptionAcknowledgement)
{
    /// <summary>The block size without a <c>blksize</c> option (RFC 1350).</summary>
    public const int DefaultBlockSize = 512;

    /// <summary>The largest block size RFC 2348 allows; a larger request is answered with this.</summary>
    public const int MaximumBlockSize = 65464;

    /// <summary>The retransmission timeout without a <c>timeout</c> option.</summary>
    public static readonly TimeSpan DefaultRetransmissionTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Answers a read request's options for a file of <paramref name="fileLength"/> bytes.
    /// </summary>
    /// <remarks>
    /// Names are matched in any case, and only the first of a repeated name counts. The
    /// server accepts <c>blksize</c> from 8 (a larger value up to <see cref="MaximumBlockSize"/>
    /// as asked, above it as <see cref="MaximumBlockSize"/>), <c>timeout</c> from 1 to 255
    /// seconds (RFC 2349), and <c>tsize</c> with any value, answered with the file's length.
    /// Every other option, and a value out of range or not a plain decimal number, is left out
    /// of the OACK, which names the accepted options in the client's order, in lower case.
    /// </remarks>
    /// <param name="options">The request's options.</param>
    /// <param name="fileLength">The length of the file being read.</param>
    /// <returns>The negotiated transfer.</returns>
    public static TftpNegotiation ForRead(IReadOnlyList<TftpOption> options, long fileLength)
    {
        var accepted = new List<TftpOption>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in options)
        {
            var name = option.Name.ToLowerInvariant();
            if (seen.Add(name) && Accept(name, option.Value, fileLength) is { } value)
            {
                accepted.Add(new TftpOption(name, value));
            }
        }

        return new TftpNegotiation(
            FindNumber(accepted, "blksize") ?? DefaultBlockSize,
            FindNumber(accepted, "timeout") is { } seconds ? TimeSpan.FromSeconds(seconds) : DefaultRetransmissionTimeout,
            accepted.Count == 0 ? null : TftpPacket.ForOptionAcknowledgement(accepted));
    }

    private static string? Accept(string name, string value, long fileLength) => name switch
    {
        "blksize" => AcceptBlockSize(value),
        "timeout" => AcceptTimeout(value),
        "tsize" => fileLength.ToString(CultureInfo.InvariantCulture),
        _ => null,
    };

    private static string? AcceptBlockSize(string value) =>
        ParseAtLeast(value, 8) is { } size ? Math.Min(size, MaximumBlockSize).ToString(CultureInfo.InvariantCulture) : null;

    private static string? AcceptTimeout(string value) =>
        ParseAtLeast(value, 1) is { } seconds && seconds <= 255 ? seconds.ToString(CultureInfo.InvariantCulture) : null;

    private static long? ParseAtLeast(string value, long minimum) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= minimum ? number : null;

    private static int? FindNumber(List<TftpOption> accepted, string name) =>
        accepted.Find(option => option.Name == name) is { } option ? int.Parse(option.Value, CultureInfo.InvariantCulture) : null;
}
