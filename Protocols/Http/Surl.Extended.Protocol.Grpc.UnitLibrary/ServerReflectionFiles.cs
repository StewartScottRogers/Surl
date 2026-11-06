namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// The two files that describe server reflection (ADR-0116 decision 9): grpc/grpc's
/// <c>grpc/reflection/v1/reflection.proto</c> and <c>grpc/reflection/v1alpha/reflection.proto</c>,
/// which declare the same messages and field numbers in different packages.
/// </summary>
public static class ServerReflectionFiles
{
    /// <summary>The service's name within its package.</summary>
    public const string ServiceName = "ServerReflection";

    /// <summary>The v1 file, package <c>grpc.reflection.v1</c>.</summary>
    public static ProtoFileDescriptor V1 { get; } = Describe("v1");

    /// <summary>The v1alpha file, package <c>grpc.reflection.v1alpha</c>, for clients older than v1.</summary>
    public static ProtoFileDescriptor V1Alpha { get; } = Describe("v1alpha");

    private static ProtoFileDescriptor Describe(string version)
    {
        var package = $"grpc.reflection.{version}";
        string Type(string name) => $".{package}.{name}";

        return new(
            $"grpc/reflection/{version}/reflection.proto",
            package,
            [
                new(
                    "ServerReflectionRequest",
                    [
                        new("host", 1, ProtoFieldType.String),
                        new("file_by_filename", 3, ProtoFieldType.String, OneofIndex: 0),
                        new("file_containing_symbol", 4, ProtoFieldType.String, OneofIndex: 0),
                        new("file_containing_extension", 5, ProtoFieldType.Message, Type("ExtensionRequest"), OneofIndex: 0),
                        new("all_extension_numbers_of_type", 6, ProtoFieldType.String, OneofIndex: 0),
                        new("list_services", 7, ProtoFieldType.String, OneofIndex: 0),
                    ],
                    ["message_request"]),
                new(
                    "ExtensionRequest",
                    [
                        new("containing_type", 1, ProtoFieldType.String),
                        new("extension_number", 2, ProtoFieldType.Int32),
                    ]),
                new(
                    "ServerReflectionResponse",
                    [
                        new("valid_host", 1, ProtoFieldType.String),
                        new("original_request", 2, ProtoFieldType.Message, Type("ServerReflectionRequest")),
                        new("file_descriptor_response", 4, ProtoFieldType.Message, Type("FileDescriptorResponse"), OneofIndex: 0),
                        new("all_extension_numbers_response", 5, ProtoFieldType.Message, Type("ExtensionNumberResponse"), OneofIndex: 0),
                        new("list_services_response", 6, ProtoFieldType.Message, Type("ListServiceResponse"), OneofIndex: 0),
                        new("error_response", 7, ProtoFieldType.Message, Type("ErrorResponse"), OneofIndex: 0),
                    ],
                    ["message_response"]),
                new("FileDescriptorResponse", [new("file_descriptor_proto", 1, ProtoFieldType.Bytes, IsRepeated: true)]),
                new(
                    "ExtensionNumberResponse",
                    [
                        new("base_type_name", 1, ProtoFieldType.String),
                        new("extension_number", 2, ProtoFieldType.Int32, IsRepeated: true),
                    ]),
                new("ListServiceResponse", [new("service", 1, ProtoFieldType.Message, Type("ServiceResponse"), IsRepeated: true)]),
                new("ServiceResponse", [new("name", 1, ProtoFieldType.String)]),
                new(
                    "ErrorResponse",
                    [
                        new("error_code", 1, ProtoFieldType.Int32),
                        new("error_message", 2, ProtoFieldType.String),
                    ]),
            ],
            [
                new(
                    ServiceName,
                    [
                        new(
                            "ServerReflectionInfo",
                            Type("ServerReflectionRequest"),
                            Type("ServerReflectionResponse"),
                            IsClientStreaming: true,
                            IsServerStreaming: true),
                    ]),
            ]);
    }
}
