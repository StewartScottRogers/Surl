using System.Buffers.Binary;
using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// The bytes the SOCKS5 server writes (RFC 1928 sections 3 and 6, RFC 1929 section 2; ADR-0121
/// decisions 3 and 4).
/// </summary>
internal static class Socks5Reply
{
    /// <summary>The method-selection reply's <c>METHOD</c> for no authentication.</summary>
    public const byte NoAuthenticationMethod = 0x00;

    /// <summary>The method-selection reply's <c>METHOD</c> for RFC 1929 username/password.</summary>
    public const byte UserPasswordMethod = 0x02;

    /// <summary>The method-selection reply's <c>METHOD</c> refusing every method offered.</summary>
    public const byte NoAcceptableMethod = 0xFF;

    /// <summary>RFC 1929's sub-negotiation version, <c>01</c>.</summary>
    public const byte UserPasswordVersion = 0x01;

    /// <summary>The RFC 1929 reply accepting the login: <c>01 00</c>.</summary>
    public static readonly ReadOnlyMemory<byte> LoginAccepted = new byte[] { UserPasswordVersion, 0x00 };

    /// <summary>The RFC 1929 reply refusing the login: <c>01 01</c>.</summary>
    public static readonly ReadOnlyMemory<byte> LoginRefused = new byte[] { UserPasswordVersion, 0x01 };

    /// <summary>
    /// The method-selection reply: <c>05</c> and <paramref name="method"/>.
    /// </summary>
    /// <param name="method">The method selected, or <see cref="NoAcceptableMethod"/>.</param>
    /// <returns>The two bytes.</returns>
    public static byte[] MethodSelected(byte method) => [SocksExchange.Socks5Version, method];

    /// <summary>
    /// A failure reply: <c>05</c>, <paramref name="code"/>, <c>00</c>, <c>ATYP 01</c> and
    /// <c>0.0.0.0:0</c>, which upstream curl accepted (ADR-0121 decision 3).
    /// </summary>
    /// <param name="code">Why the request failed.</param>
    /// <returns>The ten bytes.</returns>
    public static byte[] Failure(Socks5ReplyCode code) => [SocksExchange.Socks5Version, (byte)code, 0x00, 0x01, 0, 0, 0, 0, 0, 0];

    /// <summary>
    /// The success reply: <c>05 00 00</c> and the outbound connection's local address and port as
    /// <c>BND.ADDR</c> and <c>BND.PORT</c>, <c>ATYP 01</c> or <c>04</c>; <c>0.0.0.0:0</c> when the
    /// origin's local end point is not an IP end point.
    /// </summary>
    /// <param name="bound">The origin connection's <see cref="IConnection.LocalEndPoint"/>.</param>
    /// <returns>The reply's bytes.</returns>
    public static byte[] Succeeded(EndPoint bound)
    {
        if (bound is not IPEndPoint endPoint)
        {
            return Failure(Socks5ReplyCode.Succeeded);
        }

        var address = endPoint.Address.GetAddressBytes();
        var port = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(port, (ushort)endPoint.Port);
        byte addressType = address.Length == 4 ? (byte)0x01 : (byte)0x04;

        return [SocksExchange.Socks5Version, (byte)Socks5ReplyCode.Succeeded, 0x00, addressType, .. address, .. port];
    }

    /// <summary>
    /// The reply code for an origin the connector could not open (ADR-0121 decision 3's table).
    /// </summary>
    /// <param name="failure">Why the connector failed.</param>
    /// <returns>The code; <see cref="Socks5ReplyCode.GeneralFailure"/> for any failure the table does not name.</returns>
    public static Socks5ReplyCode CodeFor(OriginConnectionFailure failure) => failure switch
    {
        OriginConnectionFailure.NotAllowed => Socks5ReplyCode.NotAllowed,
        OriginConnectionFailure.Unreachable => Socks5ReplyCode.NetworkUnreachable,
        OriginConnectionFailure.HostNotFound => Socks5ReplyCode.HostUnreachable,
        OriginConnectionFailure.Refused => Socks5ReplyCode.ConnectionRefused,
        OriginConnectionFailure.TimedOut => Socks5ReplyCode.TtlExpired,
        _ => Socks5ReplyCode.GeneralFailure,
    };
}
