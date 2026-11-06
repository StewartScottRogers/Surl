namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Server reflection (ADR-0116 decision 9): <c>grpc.reflection.v1.ServerReflection</c> and
/// <c>grpc.reflection.v1alpha.ServerReflection</c>, each with the bidirectional-streaming
/// <c>ServerReflectionInfo</c>, answered alike from the files served. Each request read gets one
/// <c>ServerReflectionResponse</c> at once, in order; an <c>error_response</c> does not end the stream,
/// which ends OK when the client half-closes. The whole transitive closure of a file is sent every
/// time, so the stream keeps no state.
/// </summary>
public sealed class ServerReflectionService
{
    /// <summary>The v1 service's fully qualified name.</summary>
    public const string V1Name = "grpc.reflection.v1.ServerReflection";

    /// <summary>The v1alpha service's fully qualified name.</summary>
    public const string V1AlphaName = "grpc.reflection.v1alpha.ServerReflection";

    private const string MethodName = "ServerReflectionInfo";
    private const int NotFound = (int)GrpcStatusCode.NotFound;
    private const int InvalidArgument = (int)GrpcStatusCode.InvalidArgument;

    private readonly string[] serviceNames;
    private readonly Dictionary<string, ProtoFileDescriptor> filesByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProtoFileDescriptor> filesBySymbol = new(StringComparer.Ordinal);
    private readonly HashSet<string> messageTypeNames = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates reflection over <paramref name="servedFiles" /> and its own two files.
    /// </summary>
    /// <param name="servedFiles">The files describing every other service served, e.g. <see cref="EchoService.File" />.</param>
    public ServerReflectionService(IEnumerable<ProtoFileDescriptor> servedFiles)
    {
        ArgumentNullException.ThrowIfNull(servedFiles);
        ProtoFileDescriptor[] files = [.. servedFiles, ServerReflectionFiles.V1, ServerReflectionFiles.V1Alpha];
        foreach (var file in files)
        {
            filesByName[file.Name] = file;
            foreach (var symbol in file.Symbols)
            {
                filesBySymbol[symbol] = file;
            }

            messageTypeNames.UnionWith(file.MessageTypeNames);
        }

        serviceNames = [.. files.SelectMany(file => file.ServiceNames).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        Services =
        [
            new(V1Name, [new(MethodName, IsClientStreaming: true, IsServerStreaming: true, ServeAsync)]),
            new(V1AlphaName, [new(MethodName, IsClientStreaming: true, IsServerStreaming: true, ServeAsync)]),
        ];
    }

    /// <summary>The v1 and v1alpha services, ready to hand to <see cref="GrpcStreamHandler" />.</summary>
    public IReadOnlyList<GrpcService> Services { get; }

    /// <summary>
    /// Answers one <c>ServerReflectionRequest</c> with its <c>ServerReflectionResponse</c>:
    /// <c>valid_host</c> (1) the request's <c>host</c>, <c>original_request</c> (2) the request as
    /// received, then the answer to its <c>message_request</c>.
    /// </summary>
    /// <param name="request">The request's protobuf bytes.</param>
    /// <returns>The response's protobuf bytes.</returns>
    /// <exception cref="ProtobufFormatException">The request does not decode.</exception>
    internal byte[] Answer(ReadOnlyMemory<byte> request)
    {
        var (host, kind) = ReadRequest(request);
        var response = new ProtobufWriter();
        if (host.Length > 0)
        {
            response.WriteString(1, host);
        }

        response.WriteBytes(2, request.Span);
        WriteMessageResponse(response, kind);
        return response.ToArray();
    }

    private static (string Host, ProtobufField? Kind) ReadRequest(ReadOnlyMemory<byte> request)
    {
        var reader = new ProtobufReader(request);
        var host = string.Empty;
        ProtobufField? kind = null;
        while (reader.TryReadField(out var field))
        {
            if (field.FieldNumber == 1)
            {
                host = field.ReadString();
            }
            else if (field.FieldNumber is >= 3 and <= 7)
            {
                kind = field;
            }
        }

        return (host, kind);
    }

    private static void WriteError(ProtobufWriter response, int code, string message)
    {
        var error = new ProtobufWriter();
        error.WriteInt32(1, code);
        error.WriteString(2, message);
        response.WriteMessage(7, error);
    }

    private static string WithoutLeadingDot(string name) => name.StartsWith('.') ? name[1..] : name;

    private async Task<GrpcStatus> ServeAsync(GrpcCall call, CancellationToken cancellationToken)
    {
        while (await call.ReadRequestAsync(cancellationToken) is { } request)
        {
            await call.WriteReplyAsync(Answer(request));
        }

        return GrpcStatus.Ok;
    }

    private void WriteMessageResponse(ProtobufWriter response, ProtobufField? kind)
    {
        switch (kind)
        {
            case { FieldNumber: 3 } fileByFilename:
                WriteFile(response, filesByName.GetValueOrDefault(fileByFilename.ReadString()));
                break;
            case { FieldNumber: 4 } fileContainingSymbol:
                WriteFile(response, filesBySymbol.GetValueOrDefault(WithoutLeadingDot(fileContainingSymbol.ReadString())));
                break;
            case { FieldNumber: 5 } fileContainingExtension:
                _ = fileContainingExtension.ReadMessage();
                WriteError(response, NotFound, "not found");
                break;
            case { FieldNumber: 6 } allExtensionNumbersOfType:
                WriteExtensionNumbers(response, allExtensionNumbersOfType.ReadString());
                break;
            case { } listServices:
                _ = listServices.ReadString();
                WriteServiceList(response);
                break;
            default:
                WriteError(response, InvalidArgument, "no request kind");
                break;
        }
    }

    /// <summary>
    /// <c>file_descriptor_response</c> (4) with the file; it imports nothing, so it is its own
    /// transitive closure. No file is <c>NOT_FOUND</c>.
    /// </summary>
    private static void WriteFile(ProtobufWriter response, ProtoFileDescriptor? file)
    {
        if (file is null)
        {
            WriteError(response, NotFound, "not found");
            return;
        }

        var files = new ProtobufWriter();
        files.WriteBytes(1, file.ToFileDescriptorProto());
        response.WriteMessage(4, files);
    }

    /// <summary>
    /// <c>all_extension_numbers_response</c> (5) with the type's name and no numbers, as no file
    /// declares an extension; an unknown type is <c>NOT_FOUND</c>.
    /// </summary>
    private void WriteExtensionNumbers(ProtobufWriter response, string typeName)
    {
        if (!messageTypeNames.Contains(WithoutLeadingDot(typeName)))
        {
            WriteError(response, NotFound, "not found");
            return;
        }

        var numbers = new ProtobufWriter();
        numbers.WriteString(1, typeName);
        response.WriteMessage(5, numbers);
    }

    /// <summary><c>list_services_response</c> (6): every service's name, in ordinal order.</summary>
    private void WriteServiceList(ProtobufWriter response)
    {
        var list = new ProtobufWriter();
        foreach (var name in serviceNames)
        {
            var service = new ProtobufWriter();
            service.WriteString(1, name);
            list.WriteMessage(1, service);
        }

        response.WriteMessage(6, list);
    }
}
