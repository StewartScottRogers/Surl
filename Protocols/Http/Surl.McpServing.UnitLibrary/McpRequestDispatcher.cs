using System.Buffers;
using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// Answers the messages of one MCP session, one message at a time and independently of any transport:
/// the MCP lifecycle of ADR-0103 decision 1 - <c>initialize</c> with version negotiation, <c>ping</c>,
/// <c>notifications/initialized</c>, refusal of requests before <c>initialize</c>, the message limit and
/// the JSON-RPC 2.0 errors. Safe to call from several threads at once.
/// </summary>
public sealed class McpRequestDispatcher
{
    private const string InitializeMethod = "initialize";

    private const string PingMethod = "ping";

    private static readonly byte[] EmptyObject = "{}"u8.ToArray();

    private readonly McpSessionOptions _options;

    private string? _negotiatedProtocolVersion;

    /// <summary>Initializes a new instance of the <see cref="McpRequestDispatcher" /> class.</summary>
    /// <param name="options">Who the server is, what it serves, and the message limit.</param>
    public McpRequestDispatcher(McpSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Gets the revision <c>initialize</c> answered, or <see langword="null" /> until <c>initialize</c> has
    /// been answered; once set, the session accepts every request.
    /// </summary>
    public string? NegotiatedProtocolVersion => Volatile.Read(ref _negotiatedProtocolVersion);

    /// <summary>Answers one message from the client.</summary>
    /// <param name="message">The message's UTF-8 bytes, without its line terminator.</param>
    /// <returns>
    /// The response's UTF-8 JSON on one line with no terminator, or <see langword="null" /> when the message
    /// gets no response: a notification, or a response the client sent.
    /// </returns>
    public byte[]? Answer(ReadOnlyMemory<byte> message)
    {
        if (message.Length > _options.MaximumMessageBytes)
        {
            return AnswerMessageTooLong();
        }

        return JsonRpcMessageParser.Parse(message) switch
        {
            JsonRpcRequest request => AnswerRequest(request),
            JsonRpcInvalidMessage invalid => JsonRpcResponseWriter.WriteError(invalid.Id, invalid.Error),
            _ => null,
        };
    }

    /// <summary>
    /// Answers a message the transport discarded unread because it was longer than
    /// <see cref="McpSessionOptions.MaximumMessageBytes" />; the session continues.
    /// </summary>
    /// <returns>The <see cref="JsonRpcErrorCodes.InvalidRequest" /> response, with a null <c>id</c>, naming the limit.</returns>
    public byte[] AnswerMessageTooLong() =>
        JsonRpcResponseWriter.WriteError(null, JsonRpcError.MessageTooLong(_options.MaximumMessageBytes));

    private byte[] AnswerRequest(JsonRpcRequest request)
    {
        if (request.Method == InitializeMethod)
        {
            return AnswerInitialize(request);
        }

        if (request.Method == PingMethod)
        {
            return JsonRpcResponseWriter.WriteSuccess(request.Id, ParseResult(EmptyObject));
        }

        JsonRpcError error = NegotiatedProtocolVersion is null ? JsonRpcError.NotInitialized : JsonRpcError.MethodNotFound;
        return JsonRpcResponseWriter.WriteError(request.Id, error);
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
            || !value.TryGetProperty("protocolVersion", out JsonElement version)
            || version.ValueKind != JsonValueKind.String)
        {
            invalidParams = JsonRpcError.InvalidParams("protocolVersion");
            return false;
        }

        if (!value.TryGetProperty("capabilities", out JsonElement capabilities) || capabilities.ValueKind != JsonValueKind.Object)
        {
            invalidParams = JsonRpcError.InvalidParams("capabilities");
            return false;
        }

        requestedVersion = version.GetString()!;
        return true;
    }

    private JsonElement WriteInitializeResult(string version)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("protocolVersion", version);
            WriteCapabilities(writer, _options.Capabilities);
            WriteServerInfo(writer, _options.ServerInfo, version);
            if (_options.Instructions is { } instructions)
            {
                writer.WriteString("instructions", instructions);
            }

            writer.WriteEndObject();
        }

        return ParseResult(buffer.WrittenMemory);
    }

    private static void WriteCapabilities(Utf8JsonWriter writer, McpServerCapabilities capabilities)
    {
        writer.WriteStartObject("capabilities");
        if (capabilities.Resources)
        {
            writer.WriteStartObject("resources");
            writer.WriteBoolean("subscribe", false);
            writer.WriteBoolean("listChanged", false);
            writer.WriteEndObject();
        }

        if (capabilities.Tools)
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
