namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// A message a transaction controller sends its coordinator (Part 4 sections 4.2, 4.3, 4.5.2 and
/// 4.5.3): a body of one <c>amqp-value</c> holding a <c>declare</c> or a <c>discharge</c>.
/// </summary>
/// <param name="IsDeclare">Whether it is a <c>declare</c>; otherwise a <c>discharge</c>.</param>
/// <param name="GlobalId">A <c>declare</c>'s <c>global-id</c>, which asks for a distributed transaction; <see langword="null"/> when absent.</param>
/// <param name="TransactionId">A <c>discharge</c>'s <c>txn-id</c>.</param>
/// <param name="Fail">A <c>discharge</c>'s <c>fail</c>: <see langword="true"/> rolls back.</param>
internal sealed record Amqp1TransactionControl(bool IsDeclare, object? GlobalId, byte[]? TransactionId, bool Fail)
{
    /// <summary>Reads a whole message sent to a coordinator.</summary>
    /// <param name="message">The message's bytes.</param>
    /// <returns>The control message; <see langword="null"/> when the bytes do not decode or hold no single <c>declare</c> or <c>discharge</c>.</returns>
    public static Amqp1TransactionControl? Read(ReadOnlyMemory<byte> message)
    {
        try
        {
            var bodies = Amqp1MessageSection.ReadAll(message).Where(section => section.DescriptorCode == Amqp1Descriptor.AmqpValue).ToList();
            return bodies.Count == 1 && bodies[0].Described.Value is Amqp1Described control ? FromDescribed(control) : null;
        }
        catch (Amqp1DecodeException)
        {
            return null;
        }
    }

    private static Amqp1TransactionControl? FromDescribed(Amqp1Described control) => Amqp1Descriptor.CodeOf(control.Descriptor) switch
    {
        Amqp1Descriptor.Declare => new(true, Amqp1Fields.OfDescribedList(control, Amqp1Descriptor.Declare).Any(0), null, false),
        Amqp1Descriptor.Discharge => Discharge(Amqp1Fields.OfDescribedList(control, Amqp1Descriptor.Discharge)),
        _ => null,
    };

    private static Amqp1TransactionControl Discharge(Amqp1Fields fields) =>
        new(false, null, fields.Mandatory<byte[]>(0), fields.ValueOr(1, false));
}
