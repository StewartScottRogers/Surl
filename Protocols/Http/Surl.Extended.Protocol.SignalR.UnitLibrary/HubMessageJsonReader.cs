using System.Text.Json;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Reads one record of the JSON hub protocol - the bytes before a <c>0x1E</c> - into a
/// <see cref="HubMessage"/>, or finds it a protocol error (HubProtocol.md "JSON Encoding" and
/// "Errors"; ADR-0119, decisions 3 and 7).
/// </summary>
/// <remarks>
/// A record is a protocol error when it is not one JSON object; when its <c>type</c> is missing,
/// not an integer, or not 1 to 7; when a member the type requires is missing or a known member is
/// of the wrong JSON kind; when an <c>invocationId</c> or stream ID passes
/// <see cref="MaxIdLength"/> characters; when a StreamInvocation has no <c>invocationId</c>; or
/// when a Completion has both <c>result</c> and <c>error</c>. Members the server does not know,
/// and <c>headers</c>, are skipped.
/// </remarks>
public static class HubMessageJsonReader
{
    /// <summary>
    /// The most characters an <c>invocationId</c> or a stream ID may hold (ADR-0119, decision 7).
    /// </summary>
    public const int MaxIdLength = 128;

    /// <summary>
    /// Reads <paramref name="record"/>, which holds no <c>0x1E</c>.
    /// </summary>
    /// <param name="record">The record's bytes, without its terminator.</param>
    /// <param name="message">The message read, or <see langword="null"/> on a protocol error.</param>
    /// <returns>Whether the record is a message the server reads.</returns>
    public static bool TryRead(ReadOnlyMemory<byte> record, out HubMessage? message)
    {
        message = null;
        HubMessageMembers members;
        try
        {
            members = HubMessageMembers.Read(record);
        }
        catch (JsonException)
        {
            return false;
        }

        message = members.IsWellFormed ? Build(members) : null;

        return message is not null;
    }

    private static HubMessage? Build(HubMessageMembers members) => members.Type switch
    {
        (int)HubMessageType.Invocation or (int)HubMessageType.StreamInvocation => BuildInvocation(members),
        (int)HubMessageType.StreamItem => BuildStreamItem(members),
        (int)HubMessageType.Completion => BuildCompletion(members),
        (int)HubMessageType.CancelInvocation => BuildCancelInvocation(members),
        (int)HubMessageType.Ping => new HubPing(),
        (int)HubMessageType.Close => new HubClose(members.Error, members.AllowReconnect),
        _ => null,
    };

    private static HubInvocation? BuildInvocation(HubMessageMembers members)
    {
        var isStreaming = members.Type == (int)HubMessageType.StreamInvocation;
        if (members.Target is not { } target || members.Arguments is not { } arguments || (isStreaming && members.InvocationId is null))
        {
            return null;
        }

        return new HubInvocation(isStreaming, members.InvocationId, target, arguments, members.StreamIds ?? []);
    }

    private static HubCancelInvocation? BuildCancelInvocation(HubMessageMembers members) =>
        members.InvocationId is { } id ? new HubCancelInvocation(id) : null;

    private static HubStreamItem? BuildStreamItem(HubMessageMembers members) =>
        members.InvocationId is { } id && members.Item is { } item ? new HubStreamItem(id, item) : null;

    private static HubCompletion? BuildCompletion(HubMessageMembers members) =>
        members.InvocationId is { } id && !(members.Result is not null && members.Error is not null)
            ? new HubCompletion(id, members.Result, members.Error)
            : null;
}
