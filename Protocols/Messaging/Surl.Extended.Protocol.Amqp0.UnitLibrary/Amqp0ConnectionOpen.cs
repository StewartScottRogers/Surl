namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// <c>connection.open</c> (10.40; ADR-0112 decision 4): the virtual host the client asks for. Its
/// reserved <c>capabilities</c> and <c>insist</c> fields are read and ignored.
/// </summary>
/// <param name="VirtualHost">The virtual host's name.</param>
public sealed record Amqp0ConnectionOpen(string VirtualHost)
{
    /// <summary>The method <c>connection.open-ok</c> (10.41) answers it with: <c>reserved-1</c> empty.</summary>
    public static Amqp0MethodPayload OpenOk { get; } = new(Amqp0ConnectionMethodId.ClassId, Amqp0ConnectionMethodId.OpenOk, new byte[] { 0 });

    /// <summary>Reads the method's arguments.</summary>
    /// <param name="arguments">The arguments after the class and method IDs.</param>
    /// <returns>The method.</returns>
    /// <exception cref="Amqp0SyntaxException">The arguments do not decode.</exception>
    public static Amqp0ConnectionOpen Read(ReadOnlyMemory<byte> arguments)
    {
        var reader = new Amqp0WireReader(arguments);
        var virtualHost = reader.ReadShortString();
        reader.ReadShortString();
        reader.ReadBits(1);
        return new(virtualHost);
    }
}
