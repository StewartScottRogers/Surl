namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The arguments of <c>SUB &lt;subject&gt; [queue group] &lt;sid&gt;</c> (the protocol page;
/// ADR-0113 decision 4).
/// </summary>
/// <param name="Subject">The subject, as the client sent it.</param>
/// <param name="QueueGroup">The queue group; null when none was given.</param>
/// <param name="Sid">The subscription's identifier on the connection.</param>
internal sealed record NatsSubscribeArguments(string Subject, string? QueueGroup, string Sid)
{
    /// <summary>Reads SUB's arguments.</summary>
    /// <exception cref="NatsProtocolViolationException">Not two or three fields: <c>'Parser Error'</c>.</exception>
    public static NatsSubscribeArguments Parse(string arguments)
    {
        var fields = NatsOperationFields.Split(arguments);
        return fields.Length switch
        {
            2 => new NatsSubscribeArguments(fields[0], null, fields[1]),
            3 => new NatsSubscribeArguments(fields[0], fields[1], fields[2]),
            _ => throw NatsOperationFields.WrongFieldCount("SUB", fields.Length),
        };
    }
}
