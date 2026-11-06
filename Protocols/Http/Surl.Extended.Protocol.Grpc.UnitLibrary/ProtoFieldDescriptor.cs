using System.Text;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// One field of a message, as a <c>FieldDescriptorProto</c> describes it (ADR-0116 decision 9).
/// </summary>
/// <param name="Name">The field name as the <c>.proto</c> file spells it, e.g. <c>status_code</c>.</param>
/// <param name="Number">The field number.</param>
/// <param name="Type">The field's type.</param>
/// <param name="TypeName">
/// For a <see cref="ProtoFieldType.Message" /> field, the fully qualified message type with its leading
/// dot, e.g. <c>.grpc.reflection.v1.ExtensionRequest</c>; otherwise <see langword="null" />.
/// </param>
/// <param name="IsRepeated">Whether the field is <c>repeated</c> (<c>LABEL_REPEATED</c>); otherwise it is <c>LABEL_OPTIONAL</c>.</param>
/// <param name="OneofIndex">The index, in its message's <see cref="ProtoMessageDescriptor.Oneofs" />, of the <c>oneof</c> the field belongs to, or <see langword="null" />.</param>
public sealed record ProtoFieldDescriptor(
    string Name,
    int Number,
    ProtoFieldType Type,
    string? TypeName = null,
    bool IsRepeated = false,
    int? OneofIndex = null)
{
    private const int LabelOptional = 1;
    private const int LabelRepeated = 3;

    /// <summary>
    /// The field's JSON name, as protoc derives it: each underscore dropped and the letter after it
    /// upper-cased, e.g. <c>status_code</c> is <c>statusCode</c>.
    /// </summary>
    public string JsonName
    {
        get
        {
            var jsonName = new StringBuilder(Name.Length);
            var upperNext = false;
            foreach (var character in Name)
            {
                jsonName.Append(upperNext ? char.ToUpperInvariant(character) : character);
                upperNext = character == '_';
            }

            return jsonName.Replace("_", string.Empty).ToString();
        }
    }

    /// <summary>
    /// Writes the <c>FieldDescriptorProto</c>: <c>name</c> (1), <c>number</c> (3), <c>label</c> (4),
    /// <c>type</c> (5), <c>type_name</c> (6) when set, <c>oneof_index</c> (9) when set and
    /// <c>json_name</c> (10).
    /// </summary>
    /// <returns>The descriptor's writer.</returns>
    internal ProtobufWriter ToFieldDescriptorProto()
    {
        var writer = new ProtobufWriter();
        writer.WriteString(1, Name);
        writer.WriteInt32(3, Number);
        writer.WriteInt32(4, IsRepeated ? LabelRepeated : LabelOptional);
        writer.WriteInt32(5, (int)Type);
        if (TypeName is not null)
        {
            writer.WriteString(6, TypeName);
        }

        if (OneofIndex is { } oneofIndex)
        {
            writer.WriteInt32(9, oneofIndex);
        }

        writer.WriteString(10, JsonName);
        return writer;
    }
}
