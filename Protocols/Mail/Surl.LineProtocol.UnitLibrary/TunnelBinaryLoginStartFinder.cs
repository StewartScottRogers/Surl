using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Surl.Protocol.Abstractions;

namespace Surl.LineProtocol;

/// <summary>
/// Finds where a tunnel's binary login starts, for the SOCKS server and the HTTP proxy alike
/// (ADR-0182, amendments 23 and 25): at the tunnel's first byte, or at the byte after a first line
/// starting <c>PROXY </c>, which curl's <c>--haproxy-protocol</c> sends ahead of the protocol's own
/// bytes. Several such lines in a row are each followed.
/// </summary>
/// <remarks>
/// A line ends at a line feed. <see cref="TunnelBinaryLoginRedactors"/> says which bytes start a
/// binary login. One instance serves one tunnel, and is given its bytes from the first one on.
/// </remarks>
public sealed class TunnelBinaryLoginStartFinder
{
    private const byte LineFeed = (byte)'\n';

    private static ReadOnlySpan<byte> ProxyProtocolLineStart => "PROXY "u8;

    // The unfinished line's first bytes, as many as "PROXY " is long.
    private readonly ArrayBufferWriter<byte> _lineStart = new();

    // True when the next byte may start the login: first in the tunnel, or after a PROXY line.
    private bool _isAtPossibleLoginStart = true;
    private bool _isInPossibleProxyLine;

    /// <summary>
    /// Follows the tunnel's next bytes up to a binary login's first byte. Once it has returned
    /// <see langword="true"/>, or the tunnel has gone past every place a login may start, it returns
    /// <see langword="false"/>.
    /// </summary>
    /// <param name="received">The next bytes the client sent through the tunnel.</param>
    /// <param name="loginStart">
    /// When this returns <see langword="true"/>, where in <paramref name="received"/> the login starts:
    /// every byte before it is a <c>PROXY</c> line's.
    /// </param>
    /// <param name="loginRedactor">
    /// When this returns <see langword="true"/>, the redactor for that byte and every later one.
    /// </param>
    /// <returns>Whether a binary login starts in these bytes.</returns>
    public bool TryFind(
        ReadOnlySpan<byte> received,
        out int loginStart,
        [NotNullWhen(true)] out IReceivedBytesRedactor? loginRedactor)
    {
        loginStart = 0;
        loginRedactor = null;
        while (loginStart < received.Length && (_isAtPossibleLoginStart || _isInPossibleProxyLine))
        {
            if (_isAtPossibleLoginStart)
            {
                _isAtPossibleLoginStart = false;
                loginRedactor = TunnelBinaryLoginRedactors.ForFirstByte(received[loginStart]);
                if (loginRedactor is not null)
                {
                    return true;
                }

                _isInPossibleProxyLine = true;
                _lineStart.ResetWrittenCount();
            }

            loginStart += FollowLine(received[loginStart..]);
        }

        return false;
    }

    // Takes the bytes up to and including the line's line feed, or all of them, and returns how many.
    private int FollowLine(ReadOnlySpan<byte> rest)
    {
        var lineEnd = rest.IndexOf(LineFeed) + 1;
        var length = lineEnd > 0 ? lineEnd : rest.Length;
        var wanted = ProxyProtocolLineStart.Length - _lineStart.WrittenCount;
        _lineStart.Write(rest[..Math.Min(wanted, length)]);
        _isInPossibleProxyLine = lineEnd == 0;
        _isAtPossibleLoginStart = lineEnd > 0 && _lineStart.WrittenSpan.SequenceEqual(ProxyProtocolLineStart);

        return length;
    }
}
