namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// The data representation a property's value is written in (section 1.5, table 2-4).
/// </summary>
internal enum Mqtt5PropertyType
{
    /// <summary>One byte (section 1.5.1).</summary>
    Byte,

    /// <summary>A big-endian 16-bit integer (section 1.5.2).</summary>
    TwoByteInteger,

    /// <summary>A big-endian 32-bit integer (section 1.5.3).</summary>
    FourByteInteger,

    /// <summary>A Variable Byte Integer (section 1.5.5).</summary>
    VariableByteInteger,

    /// <summary>A UTF-8 Encoded String (section 1.5.4).</summary>
    Utf8String,

    /// <summary>Binary Data (section 1.5.6).</summary>
    BinaryData,

    /// <summary>A UTF-8 String Pair (section 1.5.7).</summary>
    Utf8StringPair,
}
