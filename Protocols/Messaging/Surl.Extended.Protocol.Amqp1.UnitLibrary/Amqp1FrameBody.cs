namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// A frame body read: its performative, and the payload after it (a transfer's message bytes).
/// </summary>
/// <param name="Performative">The performative; <see langword="null"/> for an empty frame, a heartbeat.</param>
/// <param name="Payload">The bytes after the performative; empty when there are none.</param>
public sealed record Amqp1FrameBody(Amqp1Performative? Performative, ReadOnlyMemory<byte> Payload)
{
    private static readonly Dictionary<ulong, Func<Amqp1Fields, Amqp1Performative>> AmqpPerformatives = new()
    {
        [Amqp1Descriptor.Open] = Amqp1Open.FromFields,
        [Amqp1Descriptor.Begin] = Amqp1Begin.FromFields,
        [Amqp1Descriptor.Attach] = Amqp1Attach.FromFields,
        [Amqp1Descriptor.Flow] = Amqp1Flow.FromFields,
        [Amqp1Descriptor.Transfer] = Amqp1Transfer.FromFields,
        [Amqp1Descriptor.Disposition] = Amqp1Disposition.FromFields,
        [Amqp1Descriptor.Detach] = Amqp1Detach.FromFields,
        [Amqp1Descriptor.End] = Amqp1End.FromFields,
        [Amqp1Descriptor.Close] = Amqp1Close.FromFields,
    };

    private static readonly Dictionary<ulong, Func<Amqp1Fields, Amqp1Performative>> SaslPerformatives = new()
    {
        [Amqp1Descriptor.SaslMechanisms] = Amqp1SaslMechanisms.FromFields,
        [Amqp1Descriptor.SaslInit] = Amqp1SaslInit.FromFields,
        [Amqp1Descriptor.SaslChallenge] = Amqp1SaslChallenge.FromFields,
        [Amqp1Descriptor.SaslResponse] = Amqp1SaslResponse.FromFields,
        [Amqp1Descriptor.SaslOutcome] = Amqp1SaslOutcome.FromFields,
    };

    /// <summary>Reads the performative at the start of <paramref name="frame"/>'s body, and the payload after it.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The performative and the payload; no performative for an empty frame.</returns>
    /// <exception cref="Amqp1DecodeException">
    /// With <see cref="Amqp1ErrorCondition.FramingError"/> when the body does not start with a
    /// described list naming a performative of the frame's type; with
    /// <see cref="Amqp1ErrorCondition.DecodeError"/> when the bytes do not decode, or a mandatory
    /// field is missing or a field is of the wrong type (ADR-0107 decision 3).
    /// </exception>
    public static Amqp1FrameBody Read(Amqp1Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.IsEmpty)
        {
            return new(null, ReadOnlyMemory<byte>.Empty);
        }

        var decoder = new Amqp1Decoder(frame.Body);
        var performatives = frame.Type == Amqp1FrameType.Amqp ? AmqpPerformatives : SaslPerformatives;
        var performative = ReadPerformative(decoder.ReadValue(), performatives)
            ?? throw new Amqp1DecodeException($"A {frame.Type} frame body is not a performative.", Amqp1ErrorCondition.FramingError);
        return new(performative, frame.Body[decoder.Position..]);
    }

    // The performative a described list names, or null when the value is not one of these performatives.
    private static Amqp1Performative? ReadPerformative(object? value, Dictionary<ulong, Func<Amqp1Fields, Amqp1Performative>> performatives)
    {
        if (value is not Amqp1Described { Value: IReadOnlyList<object?> fields } described)
        {
            return null;
        }

        return Amqp1Descriptor.CodeOf(described.Descriptor) is { } code && performatives.TryGetValue(code, out var readPerformative)
            ? readPerformative(new Amqp1Fields(fields))
            : null;
    }
}
