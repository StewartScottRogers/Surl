namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The field-value type octets of a field table or array, as RabbitMQ's 0-9-1 errata list them
/// (ADR-0112 Context), where the PDF and the XML disagree. Each member's value is its octet.
/// </summary>
public enum Amqp0FieldValueType : byte
{
    /// <summary><c>t</c>: a boolean octet, 0 false and anything else true.</summary>
    Boolean = (byte)'t',

    /// <summary><c>b</c>: a signed 8-bit integer.</summary>
    Int8 = (byte)'b',

    /// <summary><c>B</c>: an unsigned 8-bit integer.</summary>
    UInt8 = (byte)'B',

    /// <summary><c>s</c>: a signed 16-bit integer (RabbitMQ's octet; the PDF's <c>U</c>).</summary>
    Int16 = (byte)'s',

    /// <summary><c>u</c>: an unsigned 16-bit integer.</summary>
    UInt16 = (byte)'u',

    /// <summary><c>I</c>: a signed 32-bit integer.</summary>
    Int32 = (byte)'I',

    /// <summary><c>i</c>: an unsigned 32-bit integer.</summary>
    UInt32 = (byte)'i',

    /// <summary><c>l</c>: a signed 64-bit integer, as RabbitMQ and its clients write one.</summary>
    Int64 = (byte)'l',

    /// <summary><c>L</c>: a signed 64-bit integer, the PDF's <c>long-long-int</c> octet.</summary>
    Int64SpecificationOctet = (byte)'L',

    /// <summary><c>f</c>: an IEEE-754 single-precision float.</summary>
    Float32 = (byte)'f',

    /// <summary><c>d</c>: an IEEE-754 double-precision float.</summary>
    Float64 = (byte)'d',

    /// <summary><c>D</c>: a decimal, a scale octet and a long-uint value.</summary>
    Decimal = (byte)'D',

    /// <summary><c>S</c>: a long string, a long-uint length and that many octets.</summary>
    LongString = (byte)'S',

    /// <summary><c>x</c>: a byte array, a long-uint length and that many octets.</summary>
    ByteArray = (byte)'x',

    /// <summary><c>A</c>: a field array, a long-uint size and the values it holds.</summary>
    Array = (byte)'A',

    /// <summary><c>T</c>: a timestamp, seconds since the POSIX epoch as a long-long-uint.</summary>
    Timestamp = (byte)'T',

    /// <summary><c>F</c>: a nested field table.</summary>
    Table = (byte)'F',

    /// <summary><c>V</c>: void, no value and no bytes.</summary>
    Void = (byte)'V',
}
