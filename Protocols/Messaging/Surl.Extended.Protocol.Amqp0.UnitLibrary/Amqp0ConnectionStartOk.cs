namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// <c>connection.start-ok</c> (10.11; ADR-0112 decision 2): the client's properties, the mechanism
/// it chose, its response and its locale.
/// </summary>
/// <param name="ClientProperties">The <c>client-properties</c> table; kept only for the exchange log.</param>
/// <param name="Mechanism">The mechanism chosen.</param>
/// <param name="Response">The mechanism's first response.</param>
/// <param name="Locale">The locale; read and ignored.</param>
public sealed record Amqp0ConnectionStartOk(
    Amqp0FieldTable ClientProperties,
    string Mechanism,
    ReadOnlyMemory<byte> Response,
    string Locale)
{
    /// <summary>Reads the method's arguments.</summary>
    /// <param name="arguments">The arguments after the class and method IDs.</param>
    /// <returns>The method.</returns>
    /// <exception cref="Amqp0SyntaxException">The arguments do not decode.</exception>
    public static Amqp0ConnectionStartOk Read(ReadOnlyMemory<byte> arguments)
    {
        var reader = new Amqp0WireReader(arguments);
        var clientProperties = reader.ReadFieldTable();
        var mechanism = reader.ReadShortString();
        var response = reader.ReadLongString();
        return new(clientProperties, mechanism, response, reader.ReadShortString());
    }

    /// <summary>
    /// The <c>PLAIN</c> response (RFC 4616: <c>NUL login NUL password</c>) an <c>AMQPLAIN</c>
    /// <paramref name="response"/> stands for: a field table's contents without its leading size,
    /// holding <c>LOGIN</c> and <c>PASSWORD</c> as long strings (ADR-0112 decision 2).
    /// </summary>
    /// <param name="response">The <c>AMQPLAIN</c> response.</param>
    /// <returns>The <c>PLAIN</c> response, or <see langword="null"/> for any other shape, or a NUL in either value.</returns>
    public static byte[]? ToPlainResponse(ReadOnlyMemory<byte> response)
    {
        var sized = new Amqp0WireWriter();
        sized.WriteLongString(response.Span);
        Amqp0FieldTable table;
        try
        {
            table = new Amqp0WireReader(sized.ToArray()).ReadFieldTable();
        }
        catch (Amqp0SyntaxException)
        {
            return null;
        }

        if (LongStringWithoutNul(table, "LOGIN") is not { } login || LongStringWithoutNul(table, "PASSWORD") is not { } password)
        {
            return null;
        }

        return [0, .. login, 0, .. password];
    }

    private static byte[]? LongStringWithoutNul(Amqp0FieldTable table, string name) =>
        table.Find(name) is { Type: Amqp0FieldValueType.LongString, Value: byte[] value } && !value.Contains((byte)0) ? value : null;
}
