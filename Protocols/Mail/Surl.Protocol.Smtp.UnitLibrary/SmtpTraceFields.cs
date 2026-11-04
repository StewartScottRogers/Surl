using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Surl.Protocol.Smtp;

/// <summary>
/// The two trace fields the server writes ahead of a stored message (ADR-0053, decision 6; RFC
/// 5321 section 4.4): <c>Return-Path: &lt;reverse-path&gt;</c> and <c>Received: from
/// &lt;ehlo-domain&gt; (&lt;address-literal&gt;) by surl with &lt;protocol&gt;;
/// &lt;date-time&gt;</c>, each ending CRLF.
/// </summary>
internal static class SmtpTraceFields
{
    /// <summary>
    /// The trace fields' bytes.
    /// </summary>
    /// <param name="reversePath">The <c>MAIL FROM</c> path between its brackets; empty for <c>&lt;&gt;</c>.</param>
    /// <param name="heloDomain">The <c>EHLO</c> or <c>HELO</c> argument's bytes as sent.</param>
    /// <param name="remoteEndPoint">The peer's end point.</param>
    /// <param name="protocol">RFC 3848's word for the session: <c>SMTP</c>, <c>ESMTP</c>, <c>ESMTPS</c> and so on.</param>
    /// <param name="receivedAt">When the <c>354</c> was sent.</param>
    /// <returns>The two lines, CRLF after each.</returns>
    public static byte[] Build(string reversePath, byte[] heloDomain, EndPoint remoteEndPoint, string protocol, DateTimeOffset receivedAt)
    {
        var date = receivedAt.UtcDateTime.ToString("ddd, dd MMM yyyy HH:mm:ss +0000", CultureInfo.InvariantCulture);
        byte[][] parts =
        [
            Encoding.UTF8.GetBytes($"Return-Path: <{reversePath}>\r\nReceived: from "),
            heloDomain,
            Encoding.ASCII.GetBytes($" ({AddressLiteral(remoteEndPoint)}) by surl with {protocol}; {date}\r\n"),
        ];
        return [.. parts.SelectMany(part => part)];
    }

    /// <summary>
    /// RFC 5321 section 4.1.3's address literal for the peer: <c>[127.0.0.1]</c>, or
    /// <c>[IPv6:::1]</c>; an IPv4 address mapped into IPv6 is written as IPv4, and an end point
    /// that is no IP address is <c>[unknown]</c>.
    /// </summary>
    /// <param name="remoteEndPoint">The peer's end point.</param>
    /// <returns>The literal, in brackets.</returns>
    public static string AddressLiteral(EndPoint remoteEndPoint)
    {
        if (remoteEndPoint is not IPEndPoint { Address: var address })
        {
            return "[unknown]";
        }

        address = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[IPv6:{address}]" : $"[{address}]";
    }
}
