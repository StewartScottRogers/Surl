namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// How a request is answered: a response body to write and carry on, nothing written and carry
/// on (Produce with <c>acks</c> 0, ADR-0105 decision 6), or the connection closed with no bytes
/// and a note in the exchange log.
/// </summary>
public sealed record KafkaAnswer
{
    private KafkaAnswer(ReadOnlyMemory<byte>? responseBody, string? closeNote)
    {
        ResponseBody = responseBody;
        CloseNote = closeNote;
    }

    /// <summary>An answer that writes nothing and keeps the connection open.</summary>
    public static KafkaAnswer NoResponse { get; } = new(null, null);

    /// <summary>The response's own fields, after its header; null when nothing is written.</summary>
    public ReadOnlyMemory<byte>? ResponseBody { get; }

    /// <summary>What the exchange log notes when the connection is closed; null when it stays open.</summary>
    public string? CloseNote { get; }

    /// <summary>An answer that writes <paramref name="body"/> after the response header.</summary>
    /// <param name="body">The response's own fields.</param>
    /// <returns>The answer.</returns>
    public static KafkaAnswer Respond(ReadOnlyMemory<byte> body) => new(body, null);

    /// <summary>An answer that closes the connection with no bytes.</summary>
    /// <param name="note">What the exchange log notes.</param>
    /// <returns>The answer.</returns>
    public static KafkaAnswer Close(string note)
    {
        ArgumentException.ThrowIfNullOrEmpty(note);
        return new(null, note);
    }
}
