namespace Surl.Protocol.Mqtt;

/// <summary>
/// The held subscription (ADR-0014 Amendment 1), used only when the server was given a hold
/// count: a <c>SUBSCRIBE</c> is answered <c>SUBACK</c> and its retained messages, the connection
/// stays open, and each later publish is sent while the client's packets go on being read, until
/// the count-th <c>PUBLISH</c> is followed by <c>DISCONNECT</c>.
/// </summary>
internal sealed partial class MqttPacketResponder
{
    private MqttHeldSubscription? held;

    /// <summary>
    /// Whether more publishes waited for the held subscription than it may hold, which cancels
    /// the connection's reads and writes.
    /// </summary>
    public bool HasHeldBacklogOverflowed => held?.HasOverflowed == true;

    /// <summary>
    /// Reads the client's next packet. While a subscription is held, sends the publishes that
    /// arrive until the packet does.
    /// </summary>
    /// <param name="reader">The connection's packet reader.</param>
    /// <returns>What was read, or <see langword="null"/> when a live publish reached the hold
    /// count, <c>DISCONNECT</c> has been sent and the connection is to be closed.</returns>
    public async ValueTask<MqttPacketReadResult?> ReadNextPacketAsync(MqttPacketReader reader) =>
        held is { } subscription
            ? await ReadWhilePublishingAsync(reader, subscription)
            : await reader.ReadPacketAsync(context.CancellationToken);

    private async ValueTask<MqttPacketReadResult?> ReadWhilePublishingAsync(MqttPacketReader reader, MqttHeldSubscription subscription)
    {
        await using var read = new MqttPendingPacketRead(reader, subscription.Stopped);

        // One waiting publish, then the packet if it has arrived, so neither starves the other.
        while (true)
        {
            subscription.Stopped.ThrowIfCancellationRequested();
            if (subscription.TryTake(out var publish) && await SendLivePublishAsync(subscription, publish))
            {
                await DisconnectAtTheCountAsync();
                return null;
            }

            if (read.Result.IsCompleted)
            {
                return await read.Result;
            }

            await Task.WhenAny(read.Result, subscription.WaitForPublishAsync());
        }
    }

    // Whether the publish was sent and was the count-th; an expired one is not sent.
    private async ValueTask<bool> SendLivePublishAsync(MqttHeldSubscription subscription, MqttLivePublish publish)
    {
        if (publish.Message.ExpiresAt <= context.TimeProvider.GetUtcNow())
        {
            return false;
        }

        var isCountTh = subscription.CountPublishToSend();
        await WriteAsync(MqttPacketEncoder.LivePublish(publish.TopicName, publish.Message.Payload.Span));
        return isCountTh;
    }

    // The bus subscribe comes before SUBACK, so a publish made once the client has it is not missed.
    private async ValueTask<bool> HoldSubscriptionAsync(ushort packetIdentifier, List<string?> filters, int publishCount)
    {
        var isFirstSubscribe = held is null;
        held ??= new MqttHeldSubscription(bus, account, context.Scheme, publishCount, context.CancellationToken);
        var (reading, pastBoundCount) = held.Subscribe([.. filters.OfType<string>().Zip(CanonicalFiltersOf(filters))]);
        var returnCodes = ReturnCodesFor(filters, reading.Granted);
        await WriteAsync(MqttPacketEncoder.SubscribeAcknowledgement(packetIdentifier, returnCodes));
        NoteDeniedSubscriptions(reading.Granted.Count(granted => !granted) - pastBoundCount);
        NoteSubscriptionsPastTheBound(pastBoundCount);
        if (isFirstSubscribe && returnCodes.TrueForAll(returnCode => returnCode == SubscriptionFailure))
        {
            context.Log.Note("SUBSCRIBE: every topic filter was refused; sent DISCONNECT and closed.");
            return await SendAndCloseAsync(MqttPacketEncoder.Disconnect);
        }

        return await SendRetainedToHeldSubscriberAsync(held, NamedForThisScheme(reading.Matching));
    }

    private async ValueTask<bool> SendRetainedToHeldSubscriberAsync(MqttHeldSubscription subscription, List<(string TopicName, byte[] Payload)> retained)
    {
        foreach (var (topicName, payload) in retained)
        {
            var isCountTh = subscription.CountPublishToSend();
            await WriteAsync(MqttPacketEncoder.RetainedPublish(topicName, payload));
            if (isCountTh)
            {
                await DisconnectAtTheCountAsync();
                return false;
            }
        }

        context.Log.Note($"SUBSCRIBE: {retained.Count} retained messages delivered; the subscription is held open.");
        return true;
    }

    private async ValueTask DisconnectAtTheCountAsync()
    {
        context.Log.Note("The held subscription reached its publish count; sent DISCONNECT and closed.");
        await WriteAsync(MqttPacketEncoder.Disconnect);
    }

    private void NoteSubscriptionsPastTheBound(int pastBoundCount)
    {
        if (pastBoundCount > 0)
        {
            context.Log.Note($"SUBSCRIBE: {pastBoundCount} topic filters past the bus's subscriptions a connection; answered SUBACK 0x80 for each.");
        }
    }
}
