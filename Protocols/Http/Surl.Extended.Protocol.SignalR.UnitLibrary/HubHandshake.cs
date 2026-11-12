using System.Text.Json;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Judges the handshake request, the first record a client sends, and writes the handshake
/// response (HubProtocol.md "Overview"; ADR-0119, decision 3).
/// </summary>
/// <remarks>
/// The request is accepted when it is a JSON object whose <c>protocol</c> is the string
/// <c>json</c> or <c>messagepack</c> and whose <c>version</c> is the number 1 (the handshake itself is
/// always JSON, ended by <c>0x1E</c>); other members are ignored. The texts of a
/// refusal are fixed and never repeat the client's protocol name (ADR-0006).
/// </remarks>
public static class HubHandshake
{
    /// <summary>
    /// The refusal text for a protocol or version the server does not speak.
    /// </summary>
    public const string ProtocolNotAvailableText = "Requested protocol is not available.";

    /// <summary>
    /// The refusal text for a request that is not JSON, not an object, or lacks a member.
    /// </summary>
    public const string InvalidRequestText = "The handshake request is invalid.";

    /// <summary>
    /// Judges a handshake request.
    /// </summary>
    /// <param name="record">The request's record, without its <c>0x1E</c>.</param>
    /// <returns>Whether it is accepted, and if not, why.</returns>
    public static HubHandshakeOutcome Judge(ReadOnlyMemory<byte> record)
    {
        try
        {
            using var document = JsonDocument.Parse(record);

            return JudgeRoot(document.RootElement);
        }
        catch (JsonException)
        {
            return HubHandshakeOutcome.Invalid;
        }
    }

    /// <summary>
    /// The response record for <paramref name="outcome"/>: <c>{}</c> when accepted, otherwise
    /// <c>{"error":"&lt;text&gt;"}</c>, each ended by <c>0x1E</c>.
    /// </summary>
    /// <param name="outcome">The judgement.</param>
    /// <returns>The response's bytes.</returns>
    public static byte[] Response(HubHandshakeOutcome outcome) => outcome switch
    {
        HubHandshakeOutcome.Accepted or HubHandshakeOutcome.AcceptedMessagePack => HubMessageJsonWriter.WriteRecord(_ => { }),
        HubHandshakeOutcome.ProtocolNotAvailable => ErrorResponse(ProtocolNotAvailableText),
        _ => ErrorResponse(InvalidRequestText),
    };

    private static byte[] ErrorResponse(string text) =>
        HubMessageJsonWriter.WriteRecord(writer => writer.WriteString("error", text));

    private static HubHandshakeOutcome JudgeRoot(JsonElement root)
    {
        if (!TryReadMembers(root, out var protocol, out var version))
        {
            return HubHandshakeOutcome.Invalid;
        }

        if (!version.TryGetInt32(out var number) || number != 1)
        {
            return HubHandshakeOutcome.ProtocolNotAvailable;
        }

        if (protocol.ValueEquals("messagepack"))
        {
            return HubHandshakeOutcome.AcceptedMessagePack;
        }

        return protocol.ValueEquals("json") ? HubHandshakeOutcome.Accepted : HubHandshakeOutcome.ProtocolNotAvailable;
    }

    // An object with a string protocol and a number version.
    private static bool TryReadMembers(JsonElement root, out JsonElement protocol, out JsonElement version)
    {
        version = default;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("protocol", out protocol))
        {
            protocol = default;

            return false;
        }

        return root.TryGetProperty("version", out version)
            && protocol.ValueKind == JsonValueKind.String
            && version.ValueKind == JsonValueKind.Number;
    }
}
