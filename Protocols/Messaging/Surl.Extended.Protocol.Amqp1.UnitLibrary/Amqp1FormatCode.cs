namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The format codes of AMQP 1.0 Part 1 section 1.6: the constructor octet that says how the bytes
/// after it encode a value. <see cref="Described"/> starts a described value (section 1.2).
/// </summary>
public static class Amqp1FormatCode
{
    /// <summary>A described value: a descriptor, then the value it describes.</summary>
    public const byte Described = 0x00;

    /// <summary><c>null</c>, no data.</summary>
    public const byte Null = 0x40;

    /// <summary><c>boolean</c> true, no data.</summary>
    public const byte BooleanTrue = 0x41;

    /// <summary><c>boolean</c> false, no data.</summary>
    public const byte BooleanFalse = 0x42;

    /// <summary><c>uint</c> 0, no data.</summary>
    public const byte UInt0 = 0x43;

    /// <summary><c>ulong</c> 0, no data.</summary>
    public const byte ULong0 = 0x44;

    /// <summary><c>list</c> with no elements, no data.</summary>
    public const byte List0 = 0x45;

    /// <summary><c>ubyte</c>, one octet.</summary>
    public const byte UByte = 0x50;

    /// <summary><c>byte</c>, one signed octet.</summary>
    public const byte Byte = 0x51;

    /// <summary><c>uint</c> 0 to 255, one octet.</summary>
    public const byte SmallUInt = 0x52;

    /// <summary><c>ulong</c> 0 to 255, one octet.</summary>
    public const byte SmallULong = 0x53;

    /// <summary><c>int</c> -128 to 127, one signed octet.</summary>
    public const byte SmallInt = 0x54;

    /// <summary><c>long</c> -128 to 127, one signed octet.</summary>
    public const byte SmallLong = 0x55;

    /// <summary><c>boolean</c> in one octet: 0x00 false, 0x01 true.</summary>
    public const byte Boolean = 0x56;

    /// <summary><c>ushort</c>, two octets.</summary>
    public const byte UShort = 0x60;

    /// <summary><c>short</c>, two octets.</summary>
    public const byte Short = 0x61;

    /// <summary><c>uint</c>, four octets.</summary>
    public const byte UInt = 0x70;

    /// <summary><c>int</c>, four octets.</summary>
    public const byte Int = 0x71;

    /// <summary><c>float</c>, IEEE 754 binary32.</summary>
    public const byte Float = 0x72;

    /// <summary><c>char</c>, one UTF-32BE code point.</summary>
    public const byte Char = 0x73;

    /// <summary><c>decimal32</c>, IEEE 754 decimal32 in four octets.</summary>
    public const byte Decimal32 = 0x74;

    /// <summary><c>ulong</c>, eight octets.</summary>
    public const byte ULong = 0x80;

    /// <summary><c>long</c>, eight octets.</summary>
    public const byte Long = 0x81;

    /// <summary><c>double</c>, IEEE 754 binary64.</summary>
    public const byte Double = 0x82;

    /// <summary><c>timestamp</c>, milliseconds since the Unix epoch as a signed 64-bit integer.</summary>
    public const byte Timestamp = 0x83;

    /// <summary><c>decimal64</c>, IEEE 754 decimal64 in eight octets.</summary>
    public const byte Decimal64 = 0x84;

    /// <summary><c>decimal128</c>, IEEE 754 decimal128 in sixteen octets.</summary>
    public const byte Decimal128 = 0x94;

    /// <summary><c>uuid</c>, sixteen octets in RFC 4122 order.</summary>
    public const byte Uuid = 0x98;

    /// <summary><c>binary</c> up to 255 octets: a one-octet length.</summary>
    public const byte Binary8 = 0xA0;

    /// <summary><c>string</c> up to 255 octets of UTF-8: a one-octet length.</summary>
    public const byte String8 = 0xA1;

    /// <summary><c>symbol</c> up to 255 octets of ASCII: a one-octet length.</summary>
    public const byte Symbol8 = 0xA3;

    /// <summary><c>binary</c>: a four-octet length.</summary>
    public const byte Binary32 = 0xB0;

    /// <summary><c>string</c> of UTF-8: a four-octet length.</summary>
    public const byte String32 = 0xB1;

    /// <summary><c>symbol</c> of ASCII: a four-octet length.</summary>
    public const byte Symbol32 = 0xB3;

    /// <summary><c>list</c>: a one-octet size and a one-octet count.</summary>
    public const byte List8 = 0xC0;

    /// <summary><c>map</c>: a one-octet size and a one-octet count of keys and values.</summary>
    public const byte Map8 = 0xC1;

    /// <summary><c>list</c>: a four-octet size and a four-octet count.</summary>
    public const byte List32 = 0xD0;

    /// <summary><c>map</c>: a four-octet size and a four-octet count of keys and values.</summary>
    public const byte Map32 = 0xD1;

    /// <summary><c>array</c>: a one-octet size and count, one element constructor, then the elements.</summary>
    public const byte Array8 = 0xE0;

    /// <summary><c>array</c>: a four-octet size and count, one element constructor, then the elements.</summary>
    public const byte Array32 = 0xF0;
}
