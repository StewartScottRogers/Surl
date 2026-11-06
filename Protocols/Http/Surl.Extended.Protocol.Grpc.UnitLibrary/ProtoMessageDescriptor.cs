namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// One message type, as a <c>DescriptorProto</c> describes it (ADR-0116 decision 9).
/// </summary>
/// <param name="Name">The message name within its package, e.g. <c>EchoRequest</c>.</param>
/// <param name="Fields">The fields, in the order the <c>.proto</c> file declares them.</param>
/// <param name="Oneofs">The names of its <c>oneof</c>s, in order; a field's <see cref="ProtoFieldDescriptor.OneofIndex" /> indexes this list.</param>
public sealed record ProtoMessageDescriptor(string Name, IReadOnlyList<ProtoFieldDescriptor> Fields, IReadOnlyList<string> Oneofs)
{
    /// <summary>Describes a message with no <c>oneof</c>.</summary>
    /// <param name="name">The message name within its package.</param>
    /// <param name="fields">The fields, in declaration order.</param>
    public ProtoMessageDescriptor(string name, IReadOnlyList<ProtoFieldDescriptor> fields)
        : this(name, fields, [])
    {
    }

    /// <summary>
    /// Writes the <c>DescriptorProto</c>: <c>name</c> (1), each <c>field</c> (2), then each
    /// <c>oneof_decl</c> (8) as a <c>OneofDescriptorProto</c> with its <c>name</c> (1).
    /// </summary>
    /// <returns>The descriptor's writer.</returns>
    internal ProtobufWriter ToDescriptorProto()
    {
        var writer = new ProtobufWriter();
        writer.WriteString(1, Name);
        foreach (var field in Fields)
        {
            writer.WriteMessage(2, field.ToFieldDescriptorProto());
        }

        foreach (var oneof in Oneofs)
        {
            var oneofDescriptor = new ProtobufWriter();
            oneofDescriptor.WriteString(1, oneof);
            writer.WriteMessage(8, oneofDescriptor);
        }

        return writer;
    }
}
