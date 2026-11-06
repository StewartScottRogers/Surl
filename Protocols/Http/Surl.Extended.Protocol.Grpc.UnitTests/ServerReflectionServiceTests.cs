using System.Net;
using System.Text;
using Surl.Http2;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Grpc.GrpcHttp2TestFrames;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Server reflection (grpc/grpc <c>doc/server-reflection.md</c> and <c>reflection.proto</c>, protobuf's
/// <c>descriptor.proto</c>; ADR-0116 decision 9). Requests are written by hand from reflection.proto:
/// <c>3A 00</c> is field 7 (<c>list_services</c>), <c>LEN</c>, empty.
/// </summary>
[TestClass]
public sealed class ServerReflectionServiceTests
{
    private const string V1Path = "/grpc.reflection.v1.ServerReflection/ServerReflectionInfo";
    private const string V1AlphaPath = "/grpc.reflection.v1alpha.ServerReflection/ServerReflectionInfo";

    private static readonly byte[] ListServices = [0x3A, 0x00];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NullFiles_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new ServerReflectionService(null!));

    [TestMethod]
    public async Task ServerReflectionInfo_ListServices_NamesTheBuiltInServiceAndBothReflectionServicesInOrdinalOrder()
    {
        var (status, responses) = await ReflectAsync(V1Path, ListServices);

        Assert.AreEqual("grpc-status: 0", status);
        var response = Fields(responses.Single());
        Assert.HasCount(2, response);
        CollectionAssert.AreEqual(ListServices, response[0].ReadBytes().ToArray(), "original_request");
        Assert.AreEqual(6, response[1].FieldNumber, "list_services_response");
        CollectionAssert.AreEqual(
            new[] { "grpc.reflection.v1.ServerReflection", "grpc.reflection.v1alpha.ServerReflection", "surl.test.Echo" },
            Fields(response[1].ReadBytes()).Select(service => Fields(service.ReadBytes()).Single().ReadString()).ToArray());
    }

    [TestMethod]
    public async Task ServerReflectionInfo_FileContainingSymbolForTheBuiltInService_ReturnsEchoProtoWithItsServiceMethodsAndFields()
    {
        var (_, responses) = await ReflectAsync(V1Path, Request(4, "surl.test.Echo"));

        var response = Fields(responses.Single());
        Assert.AreEqual(4, response[1].FieldNumber, "file_descriptor_response");
        var file = Fields(Fields(response[1].ReadBytes()).Single().ReadBytes());
        Assert.AreEqual("surl/test/echo.proto", file[0].ReadString());
        Assert.AreEqual("surl.test", file[1].ReadString());
        Assert.AreEqual("proto3", file[^1].ReadString());

        var messages = file.Where(field => field.FieldNumber == 4).Select(field => Fields(field.ReadBytes())).ToArray();
        CollectionAssert.AreEqual(new[] { "EchoRequest", "EchoReply" }, messages.Select(message => message[0].ReadString()).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "message 1 LABEL_OPTIONAL 9 message",
                "count 2 LABEL_OPTIONAL 13 count",
                "status_code 3 LABEL_OPTIONAL 5 statusCode",
                "status_message 4 LABEL_OPTIONAL 9 statusMessage",
                "delay_milliseconds 5 LABEL_OPTIONAL 13 delayMilliseconds",
            },
            messages[0].Skip(1).Select(field => DescribeField(field.ReadBytes())).ToArray());
        CollectionAssert.AreEqual(
            new[] { "message 1 LABEL_OPTIONAL 9 message", "index 2 LABEL_OPTIONAL 13 index" },
            messages[1].Skip(1).Select(field => DescribeField(field.ReadBytes())).ToArray());

        var service = Fields(file.Single(field => field.FieldNumber == 6).ReadBytes());
        Assert.AreEqual("Echo", service[0].ReadString());
        CollectionAssert.AreEqual(
            new[]
            {
                "Echo .surl.test.EchoRequest .surl.test.EchoReply",
                "Expand .surl.test.EchoRequest .surl.test.EchoReply server_streaming",
                "Collect .surl.test.EchoRequest .surl.test.EchoReply client_streaming",
                "Chat .surl.test.EchoRequest .surl.test.EchoReply client_streaming server_streaming",
            },
            service.Skip(1).Select(method => DescribeMethod(method.ReadBytes())).ToArray());
    }

    [TestMethod]
    public async Task ServerReflectionInfo_FileByFilenameUnknown_IsErrorResponseNotFoundAndTheStreamStillEndsOk()
    {
        var (status, responses) = await ReflectAsync(V1Path, Request(3, "missing.proto"));

        Assert.AreEqual("grpc-status: 0", status);
        var response = Fields(responses.Single());
        Assert.AreEqual(7, response[1].FieldNumber, "error_response");
        var error = Fields(response[1].ReadBytes());
        Assert.AreEqual(5, error[0].ReadInt32());
        Assert.AreEqual("not found", error[1].ReadString());
    }

    [TestMethod]
    public async Task ServerReflectionInfo_ThreeRequestsOnOneStream_GetThreeResponsesInOrder()
    {
        byte[] symbol = Request(4, ".surl.test.EchoRequest.message");
        byte[] missing = Request(3, "missing.proto");

        var (status, responses) = await ReflectAsync(V1AlphaPath, ListServices, symbol, missing);

        Assert.AreEqual("grpc-status: 0", status);
        Assert.HasCount(3, responses);
        CollectionAssert.AreEqual(
            new[] { 6, 4, 7 },
            responses.Select(response => Fields(response)[1].FieldNumber).ToArray());
        CollectionAssert.AreEqual(ListServices, Fields(responses[0])[0].ReadBytes().ToArray());
        CollectionAssert.AreEqual(symbol, Fields(responses[1])[0].ReadBytes().ToArray());
        CollectionAssert.AreEqual(missing, Fields(responses[2])[0].ReadBytes().ToArray());
    }

    [TestMethod]
    public void Answer_Host_IsEchoedAsValidHostBeforeTheOriginalRequest()
    {
        byte[] request = [0x0A, 0x01, 0x68, .. ListServices];

        var response = Fields(Reflection().Answer(request));

        Assert.AreEqual(1, response[0].FieldNumber);
        Assert.AreEqual("h", response[0].ReadString());
        CollectionAssert.AreEqual(request, response[1].ReadBytes().ToArray());
    }

    [TestMethod]
    public void Answer_NoRequestKind_IsErrorResponseInvalidArgument()
    {
        var response = Fields(Reflection().Answer(new byte[] { 0x0A, 0x01, 0x68, 0x10, 0x01 }));

        var error = Fields(response[^1].ReadBytes());
        Assert.AreEqual(7, response[^1].FieldNumber);
        Assert.AreEqual(3, error[0].ReadInt32());
        Assert.AreEqual("no request kind", error[1].ReadString());
    }

    [TestMethod]
    public void Answer_FileContainingExtension_IsErrorResponseNotFound()
    {
        var response = Fields(Reflection().Answer(new byte[] { 0x2A, 0x00 }));

        Assert.AreEqual(7, response[^1].FieldNumber);
        Assert.AreEqual(5, Fields(response[^1].ReadBytes())[0].ReadInt32());
    }

    [TestMethod]
    public void Answer_AllExtensionNumbersOfAKnownType_IsItsNameAndNoNumbers()
    {
        var response = Fields(Reflection().Answer(Request(6, ".surl.test.EchoRequest")));

        Assert.AreEqual(5, response[^1].FieldNumber);
        var numbers = Fields(response[^1].ReadBytes());
        Assert.HasCount(1, numbers);
        Assert.AreEqual(".surl.test.EchoRequest", numbers[0].ReadString());
    }

    [TestMethod]
    [DataRow(4, "surl.test.Missing", DisplayName = "an unknown symbol")]
    [DataRow(6, "surl.test.Echo", DisplayName = "all_extension_numbers_of_type for a service, not a message")]
    public void Answer_UnknownName_IsErrorResponseNotFound(int kind, string name)
    {
        var response = Fields(Reflection().Answer(Request(kind, name)));

        Assert.AreEqual(7, response[^1].FieldNumber);
        Assert.AreEqual(5, Fields(response[^1].ReadBytes())[0].ReadInt32());
    }

    [TestMethod]
    public void Answer_FileByFilenameForReflectionV1_DescribesItsOneofsRepeatedFieldsAndMessageTypes()
    {
        var response = Fields(Reflection().Answer(Request(3, "grpc/reflection/v1/reflection.proto")));

        var file = Fields(Fields(response[^1].ReadBytes()).Single().ReadBytes());
        Assert.AreEqual("grpc.reflection.v1", file[1].ReadString());
        var messages = file.Where(field => field.FieldNumber == 4).Select(field => Fields(field.ReadBytes())).ToArray();
        CollectionAssert.AreEqual(
            new[]
            {
                "ServerReflectionRequest", "ExtensionRequest", "ServerReflectionResponse", "FileDescriptorResponse",
                "ExtensionNumberResponse", "ListServiceResponse", "ServiceResponse", "ErrorResponse",
            },
            messages.Select(message => message[0].ReadString()).ToArray());
        Assert.AreEqual(
            "file_containing_extension 5 LABEL_OPTIONAL 11 .grpc.reflection.v1.ExtensionRequest oneof 0 fileContainingExtension",
            DescribeField(messages[0][4].ReadBytes()));
        Assert.AreEqual("message_request", Fields(messages[0][^1].ReadBytes()).Single().ReadString());
        Assert.AreEqual(8, messages[0][^1].FieldNumber, "oneof_decl");
        Assert.AreEqual(
            "service 1 LABEL_REPEATED 11 .grpc.reflection.v1.ServiceResponse service",
            DescribeField(messages[5][1].ReadBytes()));
    }

    [TestMethod]
    public void Answer_RequestThatDoesNotDecode_Throws() =>
        Assert.ThrowsExactly<ProtobufFormatException>(() => Reflection().Answer(new byte[] { 0x3A, 0x05 }));

    [TestMethod]
    public void Services_AreV1AndV1AlphaEachWithBidirectionalServerReflectionInfo()
    {
        var services = Reflection().Services;

        CollectionAssert.AreEqual(
            new[] { ServerReflectionService.V1Name, ServerReflectionService.V1AlphaName },
            services.Select(service => service.Name).ToArray());
        Assert.IsTrue(services.All(service => service.Methods.Single() is { Name: "ServerReflectionInfo", IsClientStreaming: true, IsServerStreaming: true }));
    }

    private static ServerReflectionService Reflection() => new([EchoService.File]);

    private static byte[] Request(int kind, string text)
    {
        var writer = new ProtobufWriter();
        writer.WriteString(kind, text);
        return writer.ToArray();
    }

    private static List<ProtobufField> Fields(ReadOnlyMemory<byte> bytes)
    {
        var reader = new ProtobufReader(bytes);
        var fields = new List<ProtobufField>();
        while (reader.TryReadField(out var field))
        {
            fields.Add(field);
        }

        return fields;
    }

    /// <summary>
    /// A <c>FieldDescriptorProto</c> as text: <c>name</c>, <c>number</c>, <c>label</c>, <c>type</c>,
    /// then <c>type_name</c>, <c>oneof_index</c> when present, then <c>json_name</c>.
    /// </summary>
    private static string DescribeField(ReadOnlyMemory<byte> descriptor) =>
        string.Join(' ', Fields(descriptor).Select(field => field.FieldNumber switch
        {
            1 or 6 or 10 => field.ReadString(),
            4 => field.ReadInt32() == 3 ? "LABEL_REPEATED" : field.ReadInt32() == 1 ? "LABEL_OPTIONAL" : "?",
            9 => $"oneof {field.ReadInt32()}",
            _ => field.ReadInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
        }));

    /// <summary>A <c>MethodDescriptorProto</c> as text: <c>name</c>, <c>input_type</c>, <c>output_type</c>, and each streaming flag set.</summary>
    private static string DescribeMethod(ReadOnlyMemory<byte> descriptor) =>
        string.Join(' ', Fields(descriptor).Select(field => field.FieldNumber switch
        {
            5 => field.ReadBool() ? "client_streaming" : "?",
            6 => field.ReadBool() ? "server_streaming" : "?",
            _ => field.ReadString(),
        }));

    private async Task<(string Status, List<byte[]> Responses)> ReflectAsync(string path, params byte[][] requests)
    {
        var client = new GrpcHttp2TestFrames();
        var reflection = Reflection();
        var handler = new GrpcStreamHandler(
            [EchoService.Service, .. reflection.Services],
            UnitTestHttpAuthenticationSession.ProceedingEveryRequest,
            Context());
        byte[] frames =
        [
            .. Start,
            .. client.Call(1, path, endStream: false),
            .. requests.SelectMany(request => Data(1, Message(request), endStream: false)),
            .. Data(1, [], endStream: true),
        ];
        var connection = new InMemoryConnection([frames]);

        await new Http2ConnectionServer(handler, Context(), connection).ServeAsync();

        var (fields, body, _, _) = Response(connection.WrittenBytes, 1);
        var reader = new LengthPrefixedMessageReader(int.MaxValue, long.MaxValue);
        reader.Append(body);
        var responses = new List<byte[]>();
        while (reader.TryRead(out var message))
        {
            responses.Add(message.Payload.ToArray());
        }

        return (Text(fields).Split('\n').Single(line => line.StartsWith("grpc-status", StringComparison.Ordinal)), responses);
    }

    private ExchangeContext Context() => new(
        1,
        new ListenUrl("grpc", "127.0.0.1", 80).WithBoundPort(80),
        new IPEndPoint(IPAddress.Loopback, 80),
        new IPEndPoint(IPAddress.Loopback, 50000),
        new RecordingExchangeLog(),
        new ShortTimersOnlyTimeProvider(),
        TestContext.CancellationToken);
}
