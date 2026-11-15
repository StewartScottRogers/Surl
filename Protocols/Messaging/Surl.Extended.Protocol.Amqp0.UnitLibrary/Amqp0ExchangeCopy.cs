namespace Surl.Extended.Protocol.Amqp0;

/// <summary>One exchange as a snapshot keeps it: its declaration and its bindings at the time.</summary>
/// <param name="Name">The exchange's name.</param>
/// <param name="Type">Its type.</param>
/// <param name="Durable">Whether it was declared durable.</param>
/// <param name="Arguments">Its declared arguments, encoded.</param>
/// <param name="IsPredeclared">Whether it is one of the broker's own exchanges.</param>
/// <param name="Bindings">Its bindings, in binding order (a binding never changes, so they are shared).</param>
internal sealed record Amqp0ExchangeCopy(
    string Name,
    Amqp0ExchangeType Type,
    bool Durable,
    byte[] Arguments,
    bool IsPredeclared,
    IReadOnlyList<Amqp0Binding> Bindings)
{
    /// <summary>The bytes this copy holds.</summary>
    public long ByteCount =>
        Name.Length + Arguments.Length + Bindings.Sum(binding => (long)binding.Queue.Length + binding.Key.Length + binding.EncodedArguments.Length);

    /// <summary>Makes the live exchange this copy describes.</summary>
    public Amqp0Exchange ToExchange()
    {
        var exchange = new Amqp0Exchange(Name, Type, Durable, Arguments, IsPredeclared);
        foreach (var binding in Bindings)
        {
            exchange.Bind(binding);
        }

        return exchange;
    }
}
