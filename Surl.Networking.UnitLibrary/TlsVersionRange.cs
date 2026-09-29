using System.Security.Authentication;

namespace Surl.Networking;

/// <summary>
/// The TLS versions the server accepts (ADR-0006, section 4): every version from the lowest
/// (<c>--tlsv1.0</c>, <c>--tlsv1.1</c>, <c>--tlsv1.2</c>, <c>--tlsv1.3</c>) to the highest
/// (<c>--tls-max</c>), as the <see cref="SslProtocols"/> a server handshake enables.
/// </summary>
/// <remarks>
/// Whether the operating system then negotiates TLS 1.0 or 1.1 is the platform's; Windows 11
/// Schannel refuses both on the server side by default, and Surl does not work around it.
/// </remarks>
public sealed class TlsVersionRange
{
    // ADR-0006 section 4 lets the operator lower the minimum to TLS 1.0 or 1.1, so this one
    // place names the obsolete versions.
#pragma warning disable SYSLIB0039
    private static readonly SslProtocols[] VersionsInOrder =
        [SslProtocols.Tls, SslProtocols.Tls11, SslProtocols.Tls12, SslProtocols.Tls13];
#pragma warning restore SYSLIB0039

    /// <summary>
    /// Creates the range from <paramref name="lowest"/> to <paramref name="highest"/>, both included.
    /// </summary>
    /// <param name="lowest">The lowest version accepted: one of TLS 1.0, 1.1, 1.2 or 1.3.</param>
    /// <param name="highest">The highest version accepted: one of TLS 1.0, 1.1, 1.2 or 1.3.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is not exactly one TLS version.</exception>
    /// <exception cref="ArgumentException"><paramref name="lowest"/> is above <paramref name="highest"/>.</exception>
    public TlsVersionRange(SslProtocols lowest, SslProtocols highest)
    {
        var lowestIndex = IndexOf(lowest, nameof(lowest));
        var highestIndex = IndexOf(highest, nameof(highest));

        if (lowestIndex > highestIndex)
        {
            throw new ArgumentException($"The lowest TLS version, {lowest}, is above the highest, {highest}.", nameof(lowest));
        }

        Lowest = lowest;
        Highest = highest;
        AcceptedProtocols = VersionsInOrder[lowestIndex..(highestIndex + 1)]
            .Aggregate(SslProtocols.None, (accepted, version) => accepted | version);
    }

    /// <summary>
    /// ADR-0006 section 4's default: TLS 1.2 and TLS 1.3.
    /// </summary>
    public static TlsVersionRange Default { get; } = new(SslProtocols.Tls12, SslProtocols.Tls13);

    /// <summary>
    /// The lowest version accepted.
    /// </summary>
    public SslProtocols Lowest { get; }

    /// <summary>
    /// The highest version accepted.
    /// </summary>
    public SslProtocols Highest { get; }

    /// <summary>
    /// Every version from <see cref="Lowest"/> to <see cref="Highest"/>, combined.
    /// </summary>
    public SslProtocols AcceptedProtocols { get; }

    private static int IndexOf(SslProtocols version, string parameterName)
    {
        var index = Array.IndexOf(VersionsInOrder, version);

        return index >= 0
            ? index
            : throw new ArgumentOutOfRangeException(parameterName, version, "A TLS version bound must be exactly one of TLS 1.0, 1.1, 1.2 or 1.3.");
    }
}
