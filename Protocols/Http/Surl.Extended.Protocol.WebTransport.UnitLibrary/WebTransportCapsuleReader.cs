using System.Buffers.Binary;
using System.Text;
using System.Text.Unicode;
using Surl.Quic;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// Reads capsules (RFC 9297 section 3.2: type and length as QUIC variable-length integers, then
/// the value) from the bytes received so far on a CONNECT stream, telling a capsule that has not
/// all arrived from one whose value breaks its type's layout.
/// </summary>
public static class WebTransportCapsuleReader
{
    /// <summary>Reads the type and length that start a capsule, so its length can be judged before its value arrives.</summary>
    /// <param name="source">The bytes received so far.</param>
    /// <param name="type">The capsule type, or 0 when the header is incomplete.</param>
    /// <param name="length">The value's declared length, or 0 when the header is incomplete.</param>
    /// <param name="headerLength">The bytes the type and length took, or 0 when the header is incomplete.</param>
    /// <returns><see langword="false"/> when <paramref name="source"/> ends inside the type or the length.</returns>
    public static bool TryReadHeader(ReadOnlySpan<byte> source, out WebTransportCapsuleType type, out ulong length, out int headerLength)
    {
        type = 0;
        length = 0;
        headerLength = 0;
        if (!QuicVariableLengthInteger.TryRead(source, out var typeValue, out var typeLength)
            || !QuicVariableLengthInteger.TryRead(source[typeLength..], out length, out var lengthLength))
        {
            return false;
        }

        type = (WebTransportCapsuleType)typeValue;
        headerLength = typeLength + lengthLength;
        return true;
    }

    /// <summary>Reads one capsule from the start of <paramref name="source"/>.</summary>
    /// <param name="source">The bytes received so far.</param>
    /// <returns>The capsule and its length; incomplete while any of it has not arrived; malformed when its value breaks its type's layout.</returns>
    public static WebTransportCapsuleReadResult Read(ReadOnlySpan<byte> source)
    {
        if (!TryReadHeader(source, out var type, out var length, out var headerLength)
            || length > (ulong)(source.Length - headerLength))
        {
            return new WebTransportCapsuleReadResult(WebTransportReadStatus.Incomplete, null, 0);
        }

        var bytesRead = headerLength + (int)length;
        var capsule = Decode(type, source[headerLength..bytesRead]);
        var status = capsule is null ? WebTransportReadStatus.Malformed : WebTransportReadStatus.Complete;
        return new WebTransportCapsuleReadResult(status, capsule, bytesRead);
    }

    /// <summary>Says whether <paramref name="type"/> has its own capsule record, rather than <see cref="WebTransportUnparsedCapsule"/>.</summary>
    /// <param name="type">The capsule type.</param>
    /// <returns><see langword="true"/> when the reader decodes the type's layout.</returns>
    public static bool HasLayout(WebTransportCapsuleType type) => type switch
    {
        WebTransportCapsuleType.Datagram or WebTransportCapsuleType.CloseSession or WebTransportCapsuleType.DrainSession => true,
        _ => WebTransportLimitCapsule.IsLimitType(type),
    };

    /// <summary>Decodes one capsule's value, its type and length already read (as <c>Surl.Http3</c> hands a session's capsules over).</summary>
    /// <param name="type">The capsule type.</param>
    /// <param name="value">The whole value.</param>
    /// <returns>The capsule, or <see langword="null"/> when the value breaks its type's layout.</returns>
    internal static WebTransportCapsule? Decode(WebTransportCapsuleType type, ReadOnlySpan<byte> value) => type switch
    {
        WebTransportCapsuleType.Datagram => new WebTransportDatagramCapsule(value.ToArray()),
        WebTransportCapsuleType.CloseSession => DecodeCloseSession(value),
        WebTransportCapsuleType.DrainSession => value.IsEmpty ? new WebTransportDrainSessionCapsule() : null,
        _ when WebTransportLimitCapsule.IsLimitType(type) => DecodeLimit(type, value),
        _ => new WebTransportUnparsedCapsule(type, value.ToArray()),
    };

    private static WebTransportCloseSessionCapsule? DecodeCloseSession(ReadOnlySpan<byte> value)
    {
        const int ErrorCodeLength = WebTransportCloseSessionCapsule.ErrorCodeLength;
        if (value.Length < ErrorCodeLength
            || value.Length - ErrorCodeLength > WebTransportCloseSessionCapsule.MaxMessageLength
            || !Utf8.IsValid(value[ErrorCodeLength..]))
        {
            return null;
        }

        return new WebTransportCloseSessionCapsule(
            BinaryPrimitives.ReadUInt32BigEndian(value),
            Encoding.UTF8.GetString(value[ErrorCodeLength..]));
    }

    private static WebTransportLimitCapsule? DecodeLimit(WebTransportCapsuleType type, ReadOnlySpan<byte> value) =>
        QuicVariableLengthInteger.TryRead(value, out var limit, out var bytesRead) && bytesRead == value.Length
            ? new WebTransportLimitCapsule(type, limit)
            : null;
}
