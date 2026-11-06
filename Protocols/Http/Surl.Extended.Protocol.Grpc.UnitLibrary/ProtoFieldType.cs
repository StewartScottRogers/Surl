namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// A field's type, numbered as <c>descriptor.proto</c>'s <c>FieldDescriptorProto.Type</c> numbers it, so
/// the value is written into a <c>FieldDescriptorProto</c> as is.
/// </summary>
public enum ProtoFieldType
{
    /// <summary><c>TYPE_DOUBLE</c>.</summary>
    Double = 1,

    /// <summary><c>TYPE_FLOAT</c>.</summary>
    Float = 2,

    /// <summary><c>TYPE_INT64</c>.</summary>
    Int64 = 3,

    /// <summary><c>TYPE_UINT64</c>.</summary>
    UInt64 = 4,

    /// <summary><c>TYPE_INT32</c>.</summary>
    Int32 = 5,

    /// <summary><c>TYPE_FIXED64</c>.</summary>
    Fixed64 = 6,

    /// <summary><c>TYPE_FIXED32</c>.</summary>
    Fixed32 = 7,

    /// <summary><c>TYPE_BOOL</c>.</summary>
    Bool = 8,

    /// <summary><c>TYPE_STRING</c>.</summary>
    String = 9,

    /// <summary><c>TYPE_GROUP</c>.</summary>
    Group = 10,

    /// <summary><c>TYPE_MESSAGE</c>: the field names its message type in <see cref="ProtoFieldDescriptor.TypeName" />.</summary>
    Message = 11,

    /// <summary><c>TYPE_BYTES</c>.</summary>
    Bytes = 12,

    /// <summary><c>TYPE_UINT32</c>.</summary>
    UInt32 = 13,

    /// <summary><c>TYPE_ENUM</c>.</summary>
    Enum = 14,

    /// <summary><c>TYPE_SFIXED32</c>.</summary>
    SFixed32 = 15,

    /// <summary><c>TYPE_SFIXED64</c>.</summary>
    SFixed64 = 16,

    /// <summary><c>TYPE_SINT32</c>.</summary>
    SInt32 = 17,

    /// <summary><c>TYPE_SINT64</c>.</summary>
    SInt64 = 18,
}
