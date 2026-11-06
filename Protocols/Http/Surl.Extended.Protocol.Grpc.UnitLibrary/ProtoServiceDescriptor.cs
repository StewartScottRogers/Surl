namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// One service, as a <c>ServiceDescriptorProto</c> describes it (ADR-0116 decision 9).
/// </summary>
/// <param name="Name">The service name within its package, e.g. <c>Echo</c>.</param>
/// <param name="Methods">The methods, in declaration order.</param>
public sealed record ProtoServiceDescriptor(string Name, IReadOnlyList<ProtoMethodDescriptor> Methods)
{
    /// <summary>Writes the <c>ServiceDescriptorProto</c>: <c>name</c> (1), then each <c>method</c> (2).</summary>
    /// <returns>The descriptor's writer.</returns>
    internal ProtobufWriter ToServiceDescriptorProto()
    {
        var writer = new ProtobufWriter();
        writer.WriteString(1, Name);
        foreach (var method in Methods)
        {
            writer.WriteMessage(2, method.ToMethodDescriptorProto());
        }

        return writer;
    }
}
