namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The arguments of <c>UNSUB &lt;sid&gt; [max_msgs]</c> (the protocol page; ADR-0113 decision 4).
/// </summary>
/// <param name="Sid">The subscription's identifier on the connection.</param>
/// <param name="MaxMessages">How many messages the subscription delivers in all before it ends;
/// null to end it at once.</param>
internal sealed record NatsUnsubscribeArguments(string Sid, long? MaxMessages)
{
    /// <summary>Reads UNSUB's arguments.</summary>
    /// <exception cref="NatsProtocolViolationException">Not one or two fields, or a count that is
    /// not a non-negative decimal integer: <c>'Parser Error'</c>.</exception>
    public static NatsUnsubscribeArguments Parse(string arguments)
    {
        var fields = NatsOperationFields.Split(arguments);
        return fields.Length switch
        {
            1 => new NatsUnsubscribeArguments(fields[0], null),
            2 => new NatsUnsubscribeArguments(fields[0], NatsOperationFields.ReadCount("UNSUB", fields[1])),
            _ => throw NatsOperationFields.WrongFieldCount("UNSUB", fields.Length),
        };
    }
}
