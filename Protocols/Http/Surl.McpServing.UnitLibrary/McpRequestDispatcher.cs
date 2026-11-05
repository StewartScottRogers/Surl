using System.Buffers;
using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// Answers the messages of one MCP session, one message at a time and independently of any transport:
/// the MCP lifecycle of ADR-0103 decision 1 - <c>initialize</c> with version negotiation, <c>ping</c>,
/// <c>notifications/initialized</c>, refusal of requests before <c>initialize</c>, the message limit and
/// the JSON-RPC 2.0 errors - and the registry's tools through <c>tools/list</c> and <c>tools/call</c>.
/// Safe to call from several threads at once.
/// </summary>
public sealed class McpRequestDispatcher
{
    private const string InitializeMethod = "initialize";

    private const string PingMethod = "ping";

    private static readonly byte[] EmptyObject = "{}"u8.ToArray();

    private readonly McpServerRegistry _registry;

    private readonly McpSessionOptions _options;

    private string? _negotiatedProtocolVersion;

    /// <summary>Initializes a new instance of the <see cref="McpRequestDispatcher" /> class.</summary>
    /// <param name="registry">What the server serves; every tool is added before the first message is answered.</param>
    /// <param name="options">Who the server is, the message limit and the list page size.</param>
    public McpRequestDispatcher(McpServerRegistry registry, McpSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);
        _registry = registry;
        _options = options;
    }

    /// <summary>
    /// Gets the revision <c>initialize</c> answered, or <see langword="null" /> until <c>initialize</c> has
    /// been answered; once set, the session accepts every request.
    /// </summary>
    public string? NegotiatedProtocolVersion => Volatile.Read(ref _negotiatedProtocolVersion);

    /// <summary>Answers one message from the client.</summary>
    /// <param name="message">The message's UTF-8 bytes, without its line terminator.</param>
    /// <param name="cancellationToken">The request's cancellation token, given to a tool's handler.</param>
    /// <returns>
    /// The response's UTF-8 JSON on one line with no terminator, or <see langword="null" /> when the message
    /// gets no response: a notification, a response the client sent, or a tool call cancelled through its token.
    /// </returns>
    public Task<byte[]?> AnswerAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        if (message.Length > _options.MaximumMessageBytes)
        {
            return Task.FromResult<byte[]?>(AnswerMessageTooLong());
        }

        return JsonRpcMessageParser.Parse(message) switch
        {
            JsonRpcRequest request => AnswerRequestAsync(request, cancellationToken),
            JsonRpcInvalidMessage invalid => Task.FromResult<byte[]?>(JsonRpcResponseWriter.WriteError(invalid.Id, invalid.Error)),
            _ => Task.FromResult<byte[]?>(null),
        };
    }

    /// <summary>
    /// Answers a message the transport discarded unread because it was longer than
    /// <see cref="McpSessionOptions.MaximumMessageBytes" />; the session continues.
    /// </summary>
    /// <returns>The <see cref="JsonRpcErrorCodes.InvalidRequest" /> response, with a null <c>id</c>, naming the limit.</returns>
    public byte[] AnswerMessageTooLong() =>
        JsonRpcResponseWriter.WriteError(null, JsonRpcError.MessageTooLong(_options.MaximumMessageBytes));

    private Task<byte[]?> AnswerRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (request.Method == InitializeMethod)
        {
            return Task.FromResult<byte[]?>(AnswerInitialize(request));
        }

        if (request.Method == PingMethod)
        {
            return Task.FromResult<byte[]?>(JsonRpcResponseWriter.WriteSuccess(request.Id, ParseResult(EmptyObject)));
        }

        if (NegotiatedProtocolVersion is not { } version)
        {
            return Task.FromResult<byte[]?>(JsonRpcResponseWriter.WriteError(request.Id, JsonRpcError.NotInitialized));
        }

        return request.Method switch
        {
            McpToolMethods.ListMethod => Task.FromResult<byte[]?>(McpToolMethods.AnswerList(request, _registry, _options.ListPageSize, version)),
            McpToolMethods.CallMethod => McpToolMethods.AnswerCallAsync(request, _registry, version, cancellationToken),
            _ => Task.FromResult<byte[]?>(JsonRpcResponseWriter.WriteError(request.Id, JsonRpcError.MethodNotFound)),
        };
    }

    private byte[] AnswerInitialize(JsonRpcRequest request)
    {
        if (!TryReadInitializeParams(request.Params, out string requestedVersion, out JsonRpcError? invalidParams))
        {
            return JsonRpcResponseWriter.WriteError(request.Id, invalidParams);
        }

        string version = McpProtocolVersions.Negotiate(requestedVersion);
        if (Interlocked.CompareExchange(ref _negotiatedProtocolVersion, version, null) is not null)
        {
            return JsonRpcResponseWriter.WriteError(request.Id, JsonRpcError.AlreadyInitialized);
        }

        return JsonRpcResponseWriter.WriteSuccess(request.Id, WriteInitializeResult(version));
    }

    private static bool TryReadInitializeParams(JsonElement? parameters, out string requestedVersion, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out JsonRpcError? invalidParams)
    {
        requestedVersion = string.Empty;
        invalidParams = null;
        if (parameters is not { ValueKind: JsonValueKind.Object } value
            || !HasMember(value, "protocolVersion", JsonValueKind.String, out JsonElement version))
        {
            invalidParams = JsonRpcError.InvalidParams("protocolVersion");
            return false;
        }

        if (!HasMember(value, "capabilities", JsonValueKind.Object, out _))
        {
            invalidParams = JsonRpcError.InvalidParams("capabilities");
            return false;
        }

        requestedVersion = version.GetString()!;
        return true;
    }

    private static bool HasMember(JsonElement value, string name, JsonValueKind kind, out JsonElement member) =>
        value.TryGetProperty(name, out member) && member.ValueKind == kind;

    private JsonElement WriteInitializeResult(string version)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("protocolVersion", version);
            WriteCapabilities(writer, _registry);
            WriteServerInfo(writer, _options.ServerInfo, version);
            if (_options.Instructions is { } instructions)
            {
                writer.WriteString("instructions", instructions);
            }

            writer.WriteEndObject();
        }

        return ParseResult(buffer.WrittenMemory);
    }

    // The registry earns each capability: tools only once a tool is added (ADR-0103 decision 1).
    private static void WriteCapabilities(Utf8JsonWriter writer, McpServerRegistry registry)
    {
        writer.WriteStartObject("capabilities");
        if (registry.RegisteredTools.Count > 0)
        {
            writer.WriteStartObject("tools");
            writer.WriteBoolean("listChanged", false);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    // title is written only when 2025-06-18 was negotiated: the two older revisions do not define it (ADR-0103 decision 1).
    private static void WriteServerInfo(Utf8JsonWriter writer, McpServerInfo serverInfo, string version)
    {
        writer.WriteStartObject("serverInfo");
        writer.WriteString("name", serverInfo.Name);
        if (serverInfo.Title is { } title && version == McpProtocolVersions.Latest)
        {
            writer.WriteString("title", title);
        }

        writer.WriteString("version", serverInfo.Version);
        writer.WriteEndObject();
    }

    private static JsonElement ParseResult(ReadOnlyMemory<byte> json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
