using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// One flow's observations (RFC 7641; ADR-0097 decisions 5 and 7): it stands between the message
/// layer and the request answering, registers and deregisters observers, tells the shared
/// <see cref="CoapObservations"/> when a write changed a resource, and sends each observer's
/// notifications.
/// </summary>
/// <remarks>
/// <para>
/// A <c>GET</c> with Observe 0, and no Block2 or Block2 NUM 0, whose answer is <c>2.05</c>,
/// registers the request's token on the resource and is answered with Observe 1, or
/// the replaced entry's next number; <c>/.well-known/core</c>, a refused path, and a registration
/// past <see cref="DefaultCapacity"/> per flow or the shared capacity are answered plainly. A
/// <c>GET</c> with Observe 1 removes the entry with its token and is answered plainly.
/// </para>
/// <para>
/// A notification goes at once when a <c>PUT</c>, <c>POST</c> or <c>DELETE</c> through any flow
/// changes the resource, and when Max-Age (60 seconds) has lapsed since the last one. It is always
/// CON, composed when it is first sent so it carries the newest state and the next sequence
/// number, and answers the registration's <c>GET</c> afresh: a large resource is notified in its
/// first block, with Block2 and Size2 (RFC 7959 section 2.6). A RST, a notification given up after
/// MAX_RETRANSMIT, and a notification that is not 2.xx (a deleted file's <c>4.04</c>, sent without
/// Observe) remove the entry; the end of the flow removes every entry and sends nothing.
/// </para>
/// </remarks>
internal sealed class CoapFlowObservers : IAsyncDisposable
{
    /// <summary>The most observations one flow keeps at once: 16 (ADR-0097 decision 7).</summary>
    public const int DefaultCapacity = 16;

    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(CoapResourceAnswerer.MaxAgeSeconds);

    private static readonly HashSet<int> NotificationRequestOptions = [CoapOptionNumber.UriPath, CoapOptionNumber.UriQuery, CoapOptionNumber.Accept];

    private readonly CoapObservations shared;
    private readonly ExchangeContext context;
    private readonly Func<CoapMessage, CoapRequestOptions, CancellationToken, Task<CoapResponse>> answer;
    private readonly Func<CoapRequestOptions, string?> locate;
    private readonly Func<string, Func<ushort, CancellationToken, Task<CoapMessage>>, CancellationToken, Task<CoapDelivery>> sendConfirmable;
    private readonly int capacity;
    private readonly Lock gate = new();
    private readonly List<CoapObservation> entries = [];
    private readonly List<Task> notifying = [];

    /// <summary>Creates the observations of one flow.</summary>
    /// <param name="shared">Every observer of the process.</param>
    /// <param name="context">The exchange: its clock and log.</param>
    /// <param name="answer">Answers one request.</param>
    /// <param name="locate">The content-store location a request's Uri-Path names, or null.</param>
    /// <param name="sendConfirmable">Sends a CON of the server's through the flow's message layer.</param>
    /// <param name="capacity">The most observations the flow keeps.</param>
    public CoapFlowObservers(
        CoapObservations shared,
        ExchangeContext context,
        Func<CoapMessage, CoapRequestOptions, CancellationToken, Task<CoapResponse>> answer,
        Func<CoapRequestOptions, string?> locate,
        Func<string, Func<ushort, CancellationToken, Task<CoapMessage>>, CancellationToken, Task<CoapDelivery>> sendConfirmable,
        int capacity = DefaultCapacity)
    {
        this.shared = shared;
        this.context = context;
        this.answer = answer;
        this.locate = locate;
        this.sendConfirmable = sendConfirmable;
        this.capacity = capacity;
    }


    /// <summary>
    /// Answers <paramref name="request"/>, registering or deregistering an observer when it asks,
    /// and marking its resource changed when a write succeeded.
    /// </summary>
    /// <param name="request">A request.</param>
    /// <param name="options">Its options, read.</param>
    /// <param name="cancellationToken">Cuts the answering off.</param>
    /// <returns>The response, with Observe when the request registered.</returns>
    public async Task<CoapResponse> AnswerAsync(CoapMessage request, CoapRequestOptions options, CancellationToken cancellationToken)
    {
        if (request.Code != CoapCode.Get)
        {
            var written = await answer(request, options, cancellationToken);
            MarkChangedWhenWritten(options, written);
            return written;
        }

        if (options.Observe == 1)
        {
            Deregister(request.Token);
        }

        var response = await answer(request, options, cancellationToken);

        return options.Observe == 0 ? Register(request, options, response) : response;
    }

    /// <summary>Stops every notification of the flow, removes every entry, and sends nothing more.</summary>
    /// <returns>A task that completes when every notification has stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        Task[] stopping;

        lock (gate)
        {
            foreach (var entry in entries.ToArray())
            {
                StopWhileLocked(entry);
            }

            stopping = [.. notifying];
        }

        await Task.WhenAll(stopping);
    }

    // 2.31 Continue only asks for the next block; every other 2.xx answer to a write changed the resource.
    private void MarkChangedWhenWritten(CoapRequestOptions options, CoapResponse written)
    {
        if (written.Code.Class == 2 && written.Code != CoapCode.Continue && locate(options) is { } location)
        {
            shared.MarkChanged(location);
        }
    }

    private CoapResponse Register(CoapMessage request, CoapRequestOptions options, CoapResponse response)
    {
        if (!IsObservable(options, response))
        {
            return response;
        }

        // A GET answered 2.xx named a mapped file, so the store gave it a location.
        var location = locate(options)!;

        lock (gate)
        {
            return RegisterWhileLocked(location, NotificationRequestFor(request, options), response);
        }
    }

    private CoapResponse RegisterWhileLocked(string location, CoapMessage notificationRequest, CoapResponse response)
    {
        var replaced = entries.Find(entry => entry.Token.Span.SequenceEqual(notificationRequest.Token.Span));

        if (replaced is null && entries.Count >= capacity)
        {
            context.Log.Note($"Not observed: the flow already has {capacity} observations.");
            return response;
        }

        var observation = new CoapObservation(location, notificationRequest, replaced?.NextSequenceNumber ?? 1);

        if (replaced is not null)
        {
            StopWhileLocked(replaced);
        }

        return Admit(observation, response);
    }

    // ADR-0097 decision 5: what a GET answers 2.05 for is observable; anything else, a registration
    // asking for a later block, and /.well-known/core are served plainly (RFC 7641 section 4.1).
    private static bool IsObservable(CoapRequestOptions options, CoapResponse response) =>
        response.Code == CoapCode.Content && options.Block2 is not { Number: > 0 } && !NamesWellKnownCore(options);

    private static bool NamesWellKnownCore(CoapRequestOptions options) => options.UriPath is [".well-known", "core"];

    private CoapResponse Admit(CoapObservation observation, CoapResponse response)
    {
        if (!shared.TryAdd(observation))
        {
            context.Log.Note("Not observed: the server already has its most observations.");
            observation.Dispose();
            return response;
        }

        entries.Add(observation);
        var sequenceNumber = observation.TakeSequenceNumber();
        notifying.RemoveAll(task => task.IsCompleted);
        notifying.Add(observation.Notifying = NotifyAsync(observation));
        context.Log.Note($"Observed: {observation.Location} for token {Convert.ToHexString(observation.Token.Span)}.");

        return response with { Options = [.. response.Options, CoapOption.FromUInt(CoapOptionNumber.Observe, sequenceNumber)] };
    }

    // The registration's resource, query and Accept, and its block size as Block2 NUM 0, so a
    // notification answers what the registration asked for, never 2.03 and never a later block.
    private static CoapMessage NotificationRequestFor(CoapMessage request, CoapRequestOptions options)
    {
        var kept = request.Options.Where(option => NotificationRequestOptions.Contains(option.Number));
        CoapOption[] block = options.Block2 is { } asked
            ? [new CoapBlockOption(0, false, asked.SizeExponent).ToOption(CoapOptionNumber.Block2)]
            : [];

        return new CoapMessage(CoapMessageType.Confirmable, CoapCode.Get, request.MessageId, request.Token, [.. kept, .. block]);
    }

    private void Deregister(ReadOnlyMemory<byte> token)
    {
        lock (gate)
        {
            if (entries.Find(entry => entry.Token.Span.SequenceEqual(token.Span)) is { } entry)
            {
                context.Log.Note($"Deregistered: token {Convert.ToHexString(token.Span)} asked with Observe 1.");
                StopWhileLocked(entry);
            }
        }
    }

    private void StopWhileLocked(CoapObservation observation)
    {
        entries.Remove(observation);
        shared.Remove(observation);
        observation.Stopping.Cancel();
    }

    private async Task NotifyAsync(CoapObservation observation)
    {
        string? endingNote = null;
        try
        {
            while ((endingNote = await NotifyOnceAsync(observation, observation.Stopping.Token)) is null)
            {
            }
        }
        catch (OperationCanceledException)
        {
            // Deregistered, replaced, or the flow ended: nothing more is sent.
        }
        finally
        {
            lock (gate)
            {
                if (entries.Contains(observation))
                {
                    StopWhileLocked(observation);
                }
            }

            observation.Dispose();
        }

        // Written only after the entry is removed, so the note signals that the removal is done.
        if (endingNote is not null)
        {
            context.Log.Note(endingNote);
        }
    }

    // Waits for a change or for Max-Age to lapse, then sends one notification. Null keeps observing; a note ends it.
    private async Task<string?> NotifyOnceAsync(CoapObservation observation, CancellationToken cancellationToken)
    {
        await ChangeOrMaxAgeAsync(observation, cancellationToken);
        observation.ClearChanged();
        CoapCode? sent = null;

        var delivery = await sendConfirmable(
            "the notification",
            async (messageId, token) =>
            {
                var notification = await ComposeNotificationAsync(observation, messageId, token);
                sent = notification.Code;
                return notification;
            },
            cancellationToken);

        if (delivery == CoapDelivery.Acknowledged && sent!.Value.Class == 2)
        {
            return null;
        }

        return $"Observation ended: token {Convert.ToHexString(observation.Token.Span)}, notification {sent} {delivery}.";
    }

    private async Task ChangeOrMaxAgeAsync(CoapObservation observation, CancellationToken cancellationToken)
    {
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await Task.WhenAny(observation.Changed, Task.Delay(MaxAge, context.TimeProvider, waiting.Token));
        await waiting.CancelAsync();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<CoapMessage> ComposeNotificationAsync(CoapObservation observation, ushort messageId, CancellationToken cancellationToken)
    {
        var request = observation.NotificationRequest;
        var response = await answer(request, CoapRequestOptions.Read(request), cancellationToken);
        IReadOnlyList<CoapOption> options = response.Code.Class == 2
            ? [.. response.Options, CoapOption.FromUInt(CoapOptionNumber.Observe, observation.TakeSequenceNumber())]
            : response.Options;

        return new CoapMessage(CoapMessageType.Confirmable, response.Code, messageId, observation.Token, options, response.Payload);
    }
}
