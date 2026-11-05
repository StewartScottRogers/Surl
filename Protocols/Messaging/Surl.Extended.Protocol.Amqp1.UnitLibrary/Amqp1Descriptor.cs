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
