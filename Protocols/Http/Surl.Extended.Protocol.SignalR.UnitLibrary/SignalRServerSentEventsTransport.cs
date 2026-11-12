using System.Text;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// The Server-Sent Events transport: each record the hub sends is one event's <c>data</c> on the open
/// response, and the client's half arrives by HTTP Post (TransportProtocols.md "Server-Sent Events"; ADR-0119,
/// decision 2, amendment 1). It carries the Text transfer format only.
/// </summary>
/// <param name="posts">The client's posted payloads.</param>
/// <param name="writer">Writes a piece of the open response.</param>
internal sealed class SignalRServerSentEventsTransport(SignalRPostedPayloads posts, HttpAnswerChunkWriter writer) : ISignalRHubTransport
{
    /// <inheritdoc/>
    public SemaphoreSlim WriteGate { get; } = new(1, 1);

    /// <inheritdoc/>
    public bool CarriesBinary => false;

    /// <summary>
    /// One record as an event: each of its lines is a <c>data</c> field, and a blank line ends the event
    /// (the HTML Standard's "Server-sent events"; TransportProtocols.md "Server-Sent Events").
    /// </summary>
    /// <param name="record">The record's bytes.</param>
    /// <returns>The event's bytes.</returns>
    public static byte[] Event(byte[] record)
    {
        var text = new StringBuilder();
        foreach (var line in Encoding.UTF8.GetString(record).Split('\n'))
        {
            text.Append("data: ").Append(line.TrimEnd('\r')).Append("\r\n");
        }

        return Encoding.UTF8.GetBytes(text.Append("\r\n").ToString());
    }

    /// <inheritdoc/>
    public void Dispose() => WriteGate.Dispose();

    /// <inheritdoc/>
    public Task<SignalRTransportRead> ReadAsync(CancellationToken token) => posts.ReadAsync(token);

    /// <inheritdoc/>
    public Task WriteRecordAsync(byte[] record, bool isBinary, CancellationToken token) => writer(Event(record), token).AsTask();

    /// <inheritdoc/>
    public async Task WriteEndAsync(SignalRSessionEnd end, byte[]? hubClose, bool isBinary, CancellationToken token)
    {
        posts.Close(end);
        if (hubClose is not null)
        {
            await writer(Event(hubClose), token);
        }
    }
}
