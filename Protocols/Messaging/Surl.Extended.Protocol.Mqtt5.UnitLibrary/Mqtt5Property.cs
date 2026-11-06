namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One property in a packet's property list (section 2.2.2): its identifier and its value,
/// held in the member its table 2-4 type uses.
/// </summary>
internal sealed record Mqtt5Property
{
    private Mqtt5Property(Mqtt5PropertyIdentifier identifier, uint integer, string text, byte[] binary, string pairValue)
    {
        Identifier = identifier;
        Integer = integer;
        Text = text;
        Binary = binary;
        PairValue = pairValue;
    }

    /// <summary>
    /// The property's identifier.
    /// </summary>
    public Mqtt5PropertyIdentifier Identifier { get; }

    /// <summary>
    /// The value of a Byte, Two Byte Integer, Four Byte Integer or Variable Byte Integer
    /// property; 0 for the others.
    /// </summary>
    public uint Integer { get; }

    /// <summary>
    /// The value of a UTF-8 Encoded String property, or the name of a User Property; empty
    /// for the others.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The value of a Binary Data property; empty for the others.
    /// </summary>
    public byte[] Binary { get; }

    /// <summary>
    /// The value of a User Property; empty for the others.
    /// </summary>
    public string PairValue { get; }

    /// <summary>
    /// The data representation of the value, from table 2-4.
    /// </summary>
    public Mqtt5PropertyType Type => Mqtt5PropertyDefinition.Of(Identifier).Type;

    /// <summary>
    /// A Byte, Two Byte Integer, Four Byte Integer or Variable Byte Integer property.
    /// </summary>
    /// <param name="identifier">An identifier whose value is one of those types.</param>
    /// <param name="value">The value. Its range is checked when the property is written.</param>
    /// <returns>The property.</returns>
    /// <exception cref="ArgumentException"><paramref name="identifier"/>'s value is not an integer.</exception>
    public static Mqtt5Property FromInteger(Mqtt5PropertyIdentifier identifier, uint value)
    {
        RequireType(identifier, Mqtt5PropertyType.Byte, Mqtt5PropertyType.TwoByteInteger, Mqtt5PropertyType.FourByteInteger, Mqtt5PropertyType.VariableByteInteger);
        return new(identifier, value, string.Empty, [], string.Empty);
    }

    /// <summary>
    /// A UTF-8 Encoded String property.
    /// </summary>
    /// <param name="identifier">An identifier whose value is a UTF-8 Encoded String.</param>
    /// <param name="value">The value.</param>
    /// <returns>The property.</returns>
    /// <exception cref="ArgumentException"><paramref name="identifier"/>'s value is not a string.</exception>
    public static Mqtt5Property FromText(Mqtt5PropertyIdentifier identifier, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireType(identifier, Mqtt5PropertyType.Utf8String);
        return new(identifier, 0, value, [], string.Empty);
    }

    /// <summary>
    /// A Binary Data property.
    /// </summary>
    /// <param name="identifier">An identifier whose value is Binary Data.</param>
    /// <param name="value">The value.</param>
    /// <returns>The property.</returns>
    /// <exception cref="ArgumentException"><paramref name="identifier"/>'s value is not Binary Data.</exception>
    public static Mqtt5Property FromBinary(Mqtt5PropertyIdentifier identifier, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireType(identifier, Mqtt5PropertyType.BinaryData);
        return new(identifier, 0, string.Empty, value, string.Empty);
    }

    /// <summary>
    /// A User Property, the one UTF-8 String Pair property (section 3.1.2.11.8).
    /// </summary>
    /// <param name="name">The pair's name.</param>
    /// <param name="value">The pair's value.</param>
    /// <returns>The property.</returns>
    public static Mqtt5Property FromUserProperty(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        return new(Mqtt5PropertyIdentifier.UserProperty, 0, name, [], value);
    }

    private static void RequireType(Mqtt5PropertyIdentifier identifier, params Mqtt5PropertyType[] types)
    {
        if (!types.Contains(Mqtt5PropertyDefinition.Of(identifier).Type))
        {
            throw new ArgumentException($"Property {identifier} does not hold a value of this type.", nameof(identifier));
        }
    }
}
