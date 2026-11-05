namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// <c>connection.start</c> (10.10; ADR-0112 decision 2): version 0-9, the server's properties,
/// the SASL mechanisms offered and the locales.
/// </summary>
/// <param name="ServerProperties">The <c>server-properties</c> table.</param>
/// <param name="Mechanisms">The mechanisms offered, separated by single spaces.</param>
/// <param name="Locales">The locales, separated by single spaces.</param>
public sealed record Amqp0ConnectionStart(Amqp0FieldTable ServerProperties, string Mechanisms, string Locales)
{
    /// <summary>The method's payload: <c>version-major</c> 0, <c>version-minor</c> 9, then the fields.</summary>
    /// <returns>The method.</returns>
    public Amqp0MethodPayload ToMethod()
    {
        var writer = new Amqp0WireWriter();
        writer.WriteOctet(0);
        writer.WriteOctet(9);
        writer.WriteFieldTable(ServerProperties);
        writer.WriteLongString(System.Text.Encoding.UTF8.GetBytes(Mechanisms));
        writer.WriteLongString(System.Text.Encoding.UTF8.GetBytes(Locales));
        return new(Amqp0ConnectionMethodId.ClassId, Amqp0ConnectionMethodId.Start, writer.ToArray());
    }
}
