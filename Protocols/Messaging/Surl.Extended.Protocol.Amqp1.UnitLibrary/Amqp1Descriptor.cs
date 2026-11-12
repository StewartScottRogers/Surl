namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The descriptors of the AMQP 1.0 composite types this library reads and writes: each one's
/// numeric code (domain 0x00000000) and its symbolic name. A described list may carry either
/// (Part 1 section 1.5); <see cref="CodeOf"/> reads both.
/// </summary>
public static class Amqp1Descriptor
{
    /// <summary><c>amqp:open:list</c> (Part 2 section 2.7.1).</summary>
    public const ulong Open = 0x10;

    /// <summary><c>amqp:begin:list</c> (Part 2 section 2.7.2).</summary>
    public const ulong Begin = 0x11;

    /// <summary><c>amqp:attach:list</c> (Part 2 section 2.7.3).</summary>
    public const ulong Attach = 0x12;

    /// <summary><c>amqp:flow:list</c> (Part 2 section 2.7.4).</summary>
    public const ulong Flow = 0x13;

    /// <summary><c>amqp:transfer:list</c> (Part 2 section 2.7.5).</summary>
    public const ulong Transfer = 0x14;

    /// <summary><c>amqp:disposition:list</c> (Part 2 section 2.7.6).</summary>
    public const ulong Disposition = 0x15;

    /// <summary><c>amqp:detach:list</c> (Part 2 section 2.7.7).</summary>
    public const ulong Detach = 0x16;

    /// <summary><c>amqp:end:list</c> (Part 2 section 2.7.8).</summary>
    public const ulong End = 0x17;

    /// <summary><c>amqp:close:list</c> (Part 2 section 2.7.9).</summary>
    public const ulong Close = 0x18;

    /// <summary><c>amqp:error:list</c> (Part 2 section 2.8.14).</summary>
    public const ulong Error = 0x1D;

    /// <summary><c>amqp:received:list</c> (Part 3 section 3.4.1), a delivery state that is no outcome.</summary>
    public const ulong Received = 0x23;

    /// <summary><c>amqp:accepted:list</c> (Part 3 section 3.4.2), the outcome of a message processed.</summary>
    public const ulong Accepted = 0x24;

    /// <summary><c>amqp:rejected:list</c> (Part 3 section 3.4.3), the outcome of a message refused.</summary>
    public const ulong Rejected = 0x25;

    /// <summary><c>amqp:released:list</c> (Part 3 section 3.4.4), the outcome of a message not processed.</summary>
    public const ulong Released = 0x26;

    /// <summary><c>amqp:modified:list</c> (Part 3 section 3.4.5), the outcome of a message not processed and changed.</summary>
    public const ulong Modified = 0x27;

    /// <summary><c>amqp:source:list</c> (Part 3 section 3.5.3).</summary>
    public const ulong Source = 0x28;

    /// <summary><c>amqp:target:list</c> (Part 3 section 3.5.4).</summary>
    public const ulong Target = 0x29;

    /// <summary><c>amqp:coordinator:list</c> (Part 4 section 4.5.1), the target of a transaction controller.</summary>
    public const ulong Coordinator = 0x30;

    /// <summary><c>amqp:declare:list</c> (Part 4 section 4.5.2), the message that declares a transaction.</summary>
    public const ulong Declare = 0x31;

    /// <summary><c>amqp:discharge:list</c> (Part 4 section 4.5.3), the message that commits or rolls back a transaction.</summary>
    public const ulong Discharge = 0x32;

    /// <summary><c>amqp:declared:list</c> (Part 4 section 4.5.5), the outcome carrying a declared transaction's id.</summary>
    public const ulong Declared = 0x33;

    /// <summary><c>amqp:transactional-state:list</c> (Part 4 section 4.5.6), a delivery state naming a transaction.</summary>
    public const ulong TransactionalState = 0x34;

    /// <summary><c>amqp:sasl-mechanisms:list</c> (Part 5 section 5.3.3.1).</summary>
    public const ulong SaslMechanisms = 0x40;

    /// <summary><c>amqp:sasl-init:list</c> (Part 5 section 5.3.3.2).</summary>
    public const ulong SaslInit = 0x41;

    /// <summary><c>amqp:sasl-challenge:list</c> (Part 5 section 5.3.3.3).</summary>
    public const ulong SaslChallenge = 0x42;

    /// <summary><c>amqp:sasl-response:list</c> (Part 5 section 5.3.3.4).</summary>
    public const ulong SaslResponse = 0x43;

    /// <summary><c>amqp:sasl-outcome:list</c> (Part 5 section 5.3.3.5).</summary>
    public const ulong SaslOutcome = 0x44;

    /// <summary><c>amqp:header:list</c> (Part 3 section 3.2.1).</summary>
    public const ulong Header = 0x70;

    /// <summary><c>amqp:delivery-annotations:map</c> (Part 3 section 3.2.2).</summary>
    public const ulong DeliveryAnnotations = 0x71;

    /// <summary><c>amqp:message-annotations:map</c> (Part 3 section 3.2.3).</summary>
    public const ulong MessageAnnotations = 0x72;

    /// <summary><c>amqp:properties:list</c> (Part 3 section 3.2.4).</summary>
    public const ulong Properties = 0x73;

    /// <summary><c>amqp:application-properties:map</c> (Part 3 section 3.2.5).</summary>
    public const ulong ApplicationProperties = 0x74;

    /// <summary><c>amqp:data:binary</c> (Part 3 section 3.2.6).</summary>
    public const ulong Data = 0x75;

    /// <summary><c>amqp:amqp-sequence:list</c> (Part 3 section 3.2.7).</summary>
    public const ulong AmqpSequence = 0x76;

    /// <summary><c>amqp:amqp-value:*</c> (Part 3 section 3.2.8).</summary>
    public const ulong AmqpValue = 0x77;

    /// <summary><c>amqp:footer:map</c> (Part 3 section 3.2.9).</summary>
    public const ulong Footer = 0x78;

    private static readonly Dictionary<string, ulong> CodesByName = new(StringComparer.Ordinal)
    {
        ["amqp:open:list"] = Open,
        ["amqp:begin:list"] = Begin,
        ["amqp:attach:list"] = Attach,
        ["amqp:flow:list"] = Flow,
        ["amqp:transfer:list"] = Transfer,
        ["amqp:disposition:list"] = Disposition,
        ["amqp:detach:list"] = Detach,
        ["amqp:end:list"] = End,
        ["amqp:close:list"] = Close,
        ["amqp:error:list"] = Error,
        ["amqp:received:list"] = Received,
        ["amqp:accepted:list"] = Accepted,
        ["amqp:rejected:list"] = Rejected,
        ["amqp:released:list"] = Released,
        ["amqp:modified:list"] = Modified,
        ["amqp:source:list"] = Source,
        ["amqp:target:list"] = Target,
        ["amqp:coordinator:list"] = Coordinator,
        ["amqp:declare:list"] = Declare,
        ["amqp:discharge:list"] = Discharge,
        ["amqp:declared:list"] = Declared,
        ["amqp:transactional-state:list"] = TransactionalState,
        ["amqp:sasl-mechanisms:list"] = SaslMechanisms,
        ["amqp:sasl-init:list"] = SaslInit,
        ["amqp:sasl-challenge:list"] = SaslChallenge,
        ["amqp:sasl-response:list"] = SaslResponse,
        ["amqp:sasl-outcome:list"] = SaslOutcome,
        ["amqp:header:list"] = Header,
        ["amqp:delivery-annotations:map"] = DeliveryAnnotations,
        ["amqp:message-annotations:map"] = MessageAnnotations,
        ["amqp:properties:list"] = Properties,
        ["amqp:application-properties:map"] = ApplicationProperties,
        ["amqp:data:binary"] = Data,
        ["amqp:amqp-sequence:list"] = AmqpSequence,
        ["amqp:amqp-value:*"] = AmqpValue,
        ["amqp:footer:map"] = Footer,
    };

    /// <summary>
    /// The numeric code a descriptor names: a <see cref="ulong"/> descriptor is its own code, and a
    /// <see cref="Amqp1Symbol"/> descriptor is looked up among the names above.
    /// </summary>
    /// <param name="descriptor">The descriptor of a described value.</param>
    /// <returns>The code; <see langword="null"/> for a symbol not listed here or a descriptor of any other type.</returns>
    public static ulong? CodeOf(object? descriptor) => descriptor switch
    {
        ulong code => code,
        Amqp1Symbol symbol when CodesByName.TryGetValue(symbol.Value, out var code) => code,
        _ => null,
    };
}
