namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The kinds of MessagePack value the hub protocol tells apart (MessagePack specification, "Formats").
/// </summary>
internal enum MessagePackKind
{
    /// <summary>The first byte <c>0xc1</c>, which the specification never uses.</summary>
    Invalid,

    /// <summary>nil.</summary>
    Nil,

    /// <summary>false or true.</summary>
    Bool,

    /// <summary>An integer of any width.</summary>
    Int,

    /// <summary>float 32 or float 64.</summary>
    Float,

    /// <summary>A string.</summary>
    Str,

    /// <summary>A byte array.</summary>
    Bin,

    /// <summary>An extension value.</summary>
    Ext,

    /// <summary>An array.</summary>
    Array,

    /// <summary>A map.</summary>
    Map,
}
