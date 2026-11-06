namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// One <c>.proto</c> file, built in code and never by <c>protoc</c> (ADR-0116 decision 9), which server
/// reflection serves as its serialized <c>FileDescriptorProto</c>. A file is proto3 and imports nothing.
/// </summary>
/// <param name="Name">The file's path, e.g. <c>surl/test/echo.proto</c>.</param>
/// <param name="Package">The package, e.g. <c>surl.test</c>.</param>
/// <param name="Messages">The message types, in declaration order.</param>
/// <param name="Services">The services, in declaration order.</param>
public sealed record ProtoFileDescriptor(
    string Name,
    string Package,
    IReadOnlyList<ProtoMessageDescriptor> Messages,
    IReadOnlyList<ProtoServiceDescriptor> Services)
{
    /// <summary>The fully qualified names of the file's services, e.g. <c>surl.test.Echo</c>.</summary>
    public IEnumerable<string> ServiceNames => Services.Select(service => Qualify(service.Name));

    /// <summary>The fully qualified names of the file's message types, without a leading dot.</summary>
    public IEnumerable<string> MessageTypeNames => Messages.Select(message => Qualify(message.Name));

    /// <summary>
    /// Every fully qualified symbol the file declares, without a leading dot: each service and each of
    /// its methods (<c>surl.test.Echo.Echo</c>), each message type and each of its fields
    /// (<c>surl.test.EchoRequest.message</c>).
    /// </summary>
    public IEnumerable<string> Symbols =>
        ServiceNames
            .Concat(Services.SelectMany(service => service.Methods.Select(method => $"{Qualify(service.Name)}.{method.Name}")))
            .Concat(MessageTypeNames)
            .Concat(Messages.SelectMany(message => message.Fields.Select(messageField => $"{Qualify(message.Name)}.{messageField.Name}")));

    /// <summary>
    /// Serializes the <c>FileDescriptorProto</c>: <c>name</c> (1), <c>package</c> (2), each
    /// <c>message_type</c> (4), each <c>service</c> (6) and <c>syntax</c> (12) <c>proto3</c>.
    /// </summary>
    /// <returns>The descriptor's protobuf bytes.</returns>
    public byte[] ToFileDescriptorProto()
    {
        var writer = new ProtobufWriter();
        writer.WriteString(1, Name);
        writer.WriteString(2, Package);
        foreach (var message in Messages)
        {
            writer.WriteMessage(4, message.ToDescriptorProto());
        }

        foreach (var service in Services)
        {
            writer.WriteMessage(6, service.ToServiceDescriptorProto());
        }

        writer.WriteString(12, "proto3");
        return writer.ToArray();
    }

    private string Qualify(string name) => $"{Package}.{name}";
}
