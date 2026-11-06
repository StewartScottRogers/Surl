namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// One section of an AMQP 1.0 message (Part 3 section 3.2): its descriptor code, the described
/// value read, and its bytes exactly as they arrived, so a message can be passed on byte for byte.
/// </summary>
/// <param name="DescriptorCode">The section's descriptor, <see cref="Amqp1Descriptor.Header"/> to <see cref="Amqp1Descriptor.Footer"/>.</param>
/// <param name="Described">The section as read.</param>
/// <param name="Encoded">The section's bytes.</param>
public sealed record Amqp1MessageSection(ulong DescriptorCode, Amqp1Described Described, ReadOnlyMemory<byte> Encoded)
{
    /// <summary>Splits a whole message's bytes into its sections, in order.</summary>
    /// <param name="message">The message: the payloads of a delivery's transfers joined.</param>
    /// <returns>The sections.</returns>
    /// <exception cref="Amqp1DecodeException">The bytes do not decode, or a value in them is not a message section.</exception>
    public static IReadOnlyList<Amqp1MessageSection> ReadAll(ReadOnlyMemory<byte> message)
    {
        var decoder = new Amqp1Decoder(message);
        var sections = new List<Amqp1MessageSection>();
        while (decoder.Remaining > 0)
        {
            var start = decoder.Position;
            var described = decoder.ReadValue() as Amqp1Described
                ?? throw new Amqp1DecodeException("A value in a message is not described.");
            sections.Add(new(SectionCodeOf(described), described, message[start..decoder.Position]));
        }

        return sections;
    }

    private static ulong SectionCodeOf(Amqp1Described described) =>
        Amqp1Descriptor.CodeOf(described.Descriptor) is { } code && code >= Amqp1Descriptor.Header && code <= Amqp1Descriptor.Footer
            ? code
            : throw new Amqp1DecodeException("A value in a message is not a message section.");
}
