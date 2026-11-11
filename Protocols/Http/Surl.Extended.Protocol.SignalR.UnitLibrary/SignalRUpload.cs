namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// One <c>Collect</c> upload a connection is reading: the client's StreamItems on one stream ID,
/// counted against <c>--max-filesize</c> and kept for the Completion of a <c>Collect</c> Invocation
/// (HubProtocol.md "Upload streaming"; ADR-0119, decisions 1 and 7).
/// </summary>
/// <remarks>
/// An upload whose <c>Collect</c> is already completed - past the limit, or cancelled - is
/// abandoned: the server reads and drops its items until the client's Completion of the stream.
/// </remarks>
/// <param name="invocationId">The <c>Collect</c>'s invocation ID, or <see langword="null"/> for a non-blocking Invocation.</param>
/// <param name="isStreaming">Whether <c>Collect</c> was a StreamInvocation, which sends each item back as it arrives.</param>
internal sealed class SignalRUpload(string? invocationId, bool isStreaming)
{
    private readonly List<byte[]> items = [];
    private long itemBytes;

    /// <summary>
    /// The <c>Collect</c>'s invocation ID, or <see langword="null"/> for a non-blocking Invocation.
    /// </summary>
    public string? InvocationId => invocationId;

    /// <summary>
    /// Whether <c>Collect</c> was a StreamInvocation.
    /// </summary>
    public bool IsStreaming => isStreaming;

    /// <summary>
    /// Whether <c>Collect</c> is already completed and the upload's items are only being dropped.
    /// </summary>
    public bool IsAbandoned { get; private set; }

    /// <summary>
    /// Counts <paramref name="item"/>'s JSON bytes and keeps a copy of it, unless <c>Collect</c>
    /// streams; abandons the upload instead when the bytes pass <paramref name="maxUploadBytes"/>.
    /// </summary>
    /// <param name="item">The item, as the client sent it.</param>
    /// <param name="maxUploadBytes"><c>--max-filesize</c>; <c>0</c> means no limit.</param>
    /// <returns>Whether the upload is still within the limit.</returns>
    public bool TryAdd(HubJsonValue item, long maxUploadBytes)
    {
        itemBytes += item.Utf8Json.Length;
        if (maxUploadBytes != 0 && itemBytes > maxUploadBytes)
        {
            Abandon();

            return false;
        }

        if (!isStreaming)
        {
            items.Add(item.Utf8Json.ToArray());
        }

        return true;
    }

    /// <summary>
    /// Marks <c>Collect</c> completed and drops the items kept so far.
    /// </summary>
    public void Abandon()
    {
        IsAbandoned = true;
        items.Clear();
    }

    /// <summary>
    /// The items kept, in order, as one JSON array: the <c>result</c> of a <c>Collect</c> Invocation.
    /// </summary>
    /// <returns>The array, each item byte for byte as sent.</returns>
    public HubJsonValue ItemsAsArray()
    {
        var array = new List<byte> { (byte)'[' };
        for (var index = 0; index < items.Count; index++)
        {
            if (index > 0)
            {
                array.Add((byte)',');
            }

            array.AddRange(items[index]);
        }

        array.Add((byte)']');

        return new HubJsonValue(array.ToArray());
    }
}
