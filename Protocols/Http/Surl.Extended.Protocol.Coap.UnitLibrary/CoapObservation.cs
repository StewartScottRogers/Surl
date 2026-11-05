namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// One observer of one resource (RFC 7641 sections 3 and 4; ADR-0097 decision 5): the client's
/// token, the <c>GET</c> each notification answers afresh, the 24-bit sequence numbers, and
/// whether the resource changed since the last notification.
/// </summary>
/// <param name="location">The content-store location of the resource observed.</param>
/// <param name="notificationRequest">The <c>GET</c> each notification answers: the registration's
/// Uri-Path, Uri-Query and Accept, and its block size as Block2 NUM 0 when it sent one.</param>
/// <param name="firstSequenceNumber">The number the next message to the observer carries: 1 for a
/// new registration, or the replaced entry's next number (section 4.1).</param>
internal sealed class CoapObservation(string location, CoapMessage notificationRequest, int firstSequenceNumber) : IDisposable
{
    /// <summary>The Observe option is 24 bits (section 3.4).</summary>
    private const int SequenceNumberMask = 0xFF_FFFF;

    private readonly Lock gate = new();
    private int nextSequenceNumber = firstSequenceNumber;
    private TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The content-store location of the resource observed.</summary>
    public string Location => location;

    /// <summary>The <c>GET</c> each notification answers.</summary>
    public CoapMessage NotificationRequest => notificationRequest;

    /// <summary>The client's token, carried by every notification.</summary>
    public ReadOnlyMemory<byte> Token => notificationRequest.Token;

    /// <summary>The number the next message to the observer will carry.</summary>
    public int NextSequenceNumber => Volatile.Read(ref nextSequenceNumber);

    /// <summary>Completes when the resource changes; <see cref="ClearChanged"/> starts waiting again.</summary>
    public Task Changed
    {
        get
        {
            lock (gate)
            {
                return changed.Task;
            }
        }
    }

    /// <summary>Ends the observation's notifying.</summary>
    public CancellationTokenSource Stopping { get; } = new();

    /// <summary>The task sending the observation's notifications.</summary>
    public Task Notifying { get; set; } = Task.CompletedTask;

    /// <summary>Takes the next sequence number, modulo 2^24 (section 4.4).</summary>
    /// <returns>The number.</returns>
    public uint TakeSequenceNumber() => (uint)((Interlocked.Increment(ref nextSequenceNumber) - 1) & SequenceNumberMask);

    /// <summary>Marks the resource changed.</summary>
    public void MarkChanged()
    {
        lock (gate)
        {
            changed.TrySetResult();
        }
    }

    /// <summary>
    /// Forgets a change already marked, before a notification composes the resource's state, so a
    /// change while it is under way is marked again and sent after it (section 4.5).
    /// </summary>
    public void ClearChanged()
    {
        lock (gate)
        {
            if (changed.Task.IsCompleted)
            {
                changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    /// <summary>Releases <see cref="Stopping"/>.</summary>
    public void Dispose() => Stopping.Dispose();
}
