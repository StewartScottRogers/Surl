namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// One method of a service, as a <c>MethodDescriptorProto</c> describes it (ADR-0116 decision 9).
/// </summary>
/// <param name="Name">The method name, e.g. <c>Echo</c>.</param>
/// <param name="InputType">The fully qualified request message type with its leading dot, e.g. <c>.surl.test.EchoRequest</c>.</param>
/// <param name="OutputType">The fully qualified reply message type with its leading dot.</param>
/// <param name="IsClientStreaming">Whether the request is a <c>stream</c>.</param>
/// <param name="IsServerStreaming">Whether the reply is a <c>stream</c>.</param>
public sealed record ProtoMethodDescriptor(string Name, string InputType, string OutputType, bool IsClientStreaming, bool IsServerStreaming)
{
    /// <summary>
    /// Writes the <c>MethodDescriptorProto</c>: <c>name</c> (1), <c>input_type</c> (2),
    /// <c>output_type</c> (3), and <c>client_streaming</c> (5) and <c>server_streaming</c> (6) only when
    /// true, as protoc leaves a <c>bool</c> at its default out.
    /// </summary>
    /// <returns>The descriptor's writer.</returns>
    internal ProtobufWriter ToMethodDescriptorProto()
    {
        var writer = new ProtobufWriter();
        writer.WriteString(1, Name);
        writer.WriteString(2, InputType);
        writer.WriteString(3, OutputType);
        if (IsClientStreaming)
        {
            writer.WriteBool(5, true);
        }

        if (IsServerStreaming)
        {
            writer.WriteBool(6, true);
        }

        return writer;
    }
}
