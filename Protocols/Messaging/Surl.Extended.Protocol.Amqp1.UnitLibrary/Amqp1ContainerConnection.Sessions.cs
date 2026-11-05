namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Sessions: <c>begin</c> and <c>end</c>, and the frames of a session (Part 2 sections 2.5, 2.7.2 and
/// 2.7.8; ADR-0107 decision 4).
/// </summary>
public sealed partial class Amqp1ContainerConnection
{
    private async ValueTask<bool> AnswerBeginAsync(ushort channel, Amqp1Begin begin)
    {
        if (RefuseBegin(channel, begin) is { } refusal)
        {
            return await CloseWithErrorAsync(Error(Amqp1ErrorCondition.FramingError, refusal));
        }

        if (LowestFreeOutgoingChannel(channel) is not { } outgoingChannel)
        {
            return await CloseWithErrorAsync(Error(Amqp1ErrorCondition.FramingError, "no channel free within channel-max"));
        }

        sessionsByIncomingChannel.Add(channel, new Amqp1Session(channel, outgoingChannel, begin.HandleMax));
        await WriteAsync(outgoingChannel, new Amqp1Begin(0, ContainerWindow, ContainerWindow)
        {
            RemoteChannel = channel,
            HandleMax = ContainerHandleMax,
        });
        return true;
    }

    private string? RefuseBegin(ushort channel, Amqp1Begin begin)
    {
        if (begin.RemoteChannel is not null)
        {
            return "a begin answering a session the container did not begin";
        }

        if (channel > ContainerChannelMax)
        {
            return "a begin on a channel above channel-max";
        }

        return sessionsByIncomingChannel.ContainsKey(channel) ? "a begin on a channel in use" : null;
    }

    // The client's channel number when it is free, otherwise the lowest free one, never above either channel-max.
    private ushort? LowestFreeOutgoingChannel(ushort incomingChannel)
    {
        var limit = Math.Min(ContainerChannelMax, clientChannelMax);
        var used = sessionsByIncomingChannel.Values.Select(session => session.OutgoingChannel).ToHashSet();
        if (incomingChannel <= limit && !used.Contains(incomingChannel))
        {
            return incomingChannel;
        }

        for (ushort channel = 0; channel <= limit; channel++)
        {
            if (!used.Contains(channel))
            {
                return channel;
            }
        }

        return null;
    }

    private ValueTask<bool> AnswerInSessionAsync(Amqp1Session session, Amqp1Performative performative)
    {
        if (session.IsEnding)
        {
            return ValueTask.FromResult(performative is not Amqp1End || ForgetSession(session));
        }

        return performative switch
        {
            Amqp1End end => AnswerEndAsync(session, end),
            Amqp1Attach attach => AnswerAttachAsync(session, attach),
            Amqp1Detach detach => AnswerDetachAsync(session, detach),
            Amqp1Transfer transfer => RequireAttachedAsync(session, transfer.Handle),
            Amqp1Flow { Handle: { } handle } => RequireAttachedAsync(session, handle),

            // A session flow and a disposition are BL-513's.
            _ => ValueTask.FromResult(true),
        };
    }

    private async ValueTask<bool> AnswerEndAsync(Amqp1Session session, Amqp1End end)
    {
        NoteClientError("end", end.Error);
        ForgetSession(session);
        await WriteAsync(session.OutgoingChannel, new Amqp1End());
        return true;
    }

    // Part 2 section 2.5.5: end with an error, then discard the session's frames until the client's end.
    private async ValueTask<bool> EndSessionWithErrorAsync(Amqp1Session session, Amqp1Error error)
    {
        context.Log.Note($"Session on channel {session.IncomingChannel} ended with {error.Condition.Value}: {error.Description}.");
        session.IsEnding = true;
        await WriteAsync(session.OutgoingChannel, new Amqp1End(error));
        return true;
    }

    private bool ForgetSession(Amqp1Session session) => sessionsByIncomingChannel.Remove(session.IncomingChannel);
}
