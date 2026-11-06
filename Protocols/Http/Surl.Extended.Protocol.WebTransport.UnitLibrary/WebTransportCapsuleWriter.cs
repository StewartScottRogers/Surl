using Surl.Quic;

namespace Surl.Extended.Protocol.WebTransport;

/// <summary>Writes a capsule as RFC 9297 section 3.2 lays it out: type, length, value.</summary>
public static class WebTransportCapsuleWriter
{
    /// <summary>Returns <paramref name="capsule"/>'s bytes, with the shortest encoding of its type and length.</summary>
    /// <param name="capsule">The capsule to write.</param>
    /// <returns>The capsule's bytes.</returns>
    public static byte[] Write(WebTransportCapsule capsule)
    {
        ArgumentNullException.ThrowIfNull(capsule);
        var value = capsule.EncodeValue();
        var type = (ulong)capsule.Type;
        var length = (ulong)value.Length;
        var typeLength = QuicVariableLengthInteger.EncodedLength(type);
        var lengthLength = QuicVariableLengthInteger.EncodedLength(length);
        var bytes = new byte[typeLength + lengthLength + value.Length];
        QuicVariableLengthInteger.Write(bytes, type);
        QuicVariableLengthInteger.Write(bytes.AsSpan(typeLength), length);
        value.CopyTo(bytes.AsSpan(typeLength + lengthLength));
        return bytes;
    }
}
