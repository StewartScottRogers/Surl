namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// One binding of a queue to an exchange the broker holds (ADR-0112 decision 5): the queue, the
/// binding key, and the arguments, which only a headers exchange matches and keeps; every other
/// exchange's bindings carry the empty table.
/// </summary>
internal sealed class Amqp0Binding
{
    public Amqp0Binding(string queue, string key, Amqp0FieldTable arguments)
    {
        Queue = queue;
        Key = key;
        Arguments = arguments;
        var writer = new Amqp0WireWriter();
        writer.WriteFieldTable(arguments);
        EncodedArguments = writer.ToArray();
    }

    public string Queue { get; }

    public string Key { get; }

    public Amqp0FieldTable Arguments { get; }

    /// <summary>The arguments as a field table on the wire: what tells two bindings apart and what is persisted.</summary>
    public byte[] EncodedArguments { get; }

    /// <summary>Whether <paramref name="other"/> is the same binding: same queue, key and arguments.</summary>
    public bool IsSameAs(Amqp0Binding other) =>
        Queue == other.Queue && Key == other.Key && EncodedArguments.AsSpan().SequenceEqual(other.EncodedArguments);
}
