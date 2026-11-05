namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// The wire types of the protobuf encoding guide, the low three bits of every field's tag.
/// </summary>
public enum ProtobufWireType
{
    /// <summary><c>VARINT</c>: int32, int64, uint32, uint64, sint32, sint64, bool and enum.</summary>
    Varint = 0,

    /// <summary><c>I64</c>: fixed64, sfixed64 and double, 8 bytes little-endian.</summary>
    Fixed64 = 1,

    /// <summary><c>LEN</c>: string, bytes, embedded messages and packed repeated fields, a varint length then the bytes.</summary>
    LengthDelimited = 2,

    /// <summary><c>SGROUP</c>: the start of a deprecated group, read only to skip it.</summary>
    StartGroup = 3,

    /// <summary><c>EGROUP</c>: the end of a deprecated group.</summary>
    EndGroup = 4,

    /// <summary><c>I32</c>: fixed32, sfixed32 and float, 4 bytes little-endian.</summary>
    Fixed32 = 5,
}
