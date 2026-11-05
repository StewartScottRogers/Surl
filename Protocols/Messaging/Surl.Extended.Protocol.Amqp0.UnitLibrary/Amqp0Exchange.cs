namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// One exchange the broker holds itself (ADR-0112 decision 5): a pre-declared one or one a client
/// declared, with its bindings. <c>amq.topic</c>'s bindings are the bus's, so it holds none here.
/// </summary>
/// <param name="name">The exchange's name.</param>
/// <param name="type">Its type.</param>
/// <param name="durable">Whether it was declared durable.</param>
/// <param name="arguments">Its declared arguments, encoded, kept only to compare a redeclaration.</param>
/// <param name="isPredeclared">Whether it is one of the broker's own <c>amq.</c> exchanges.</param>
internal sealed class Amqp0Exchange(string name, Amqp0ExchangeType type, bool durable, byte[] arguments, bool isPredeclared)
{
    private readonly List<(string Queue, string Key)> bindings = [];

    public string Name { get; } = name;

    public Amqp0ExchangeType Type { get; } = type;

    public bool Durable { get; } = durable;

    public byte[] Arguments { get; } = arguments;

    public bool IsPredeclared { get; } = isPredeclared;

    public int BindingCount => bindings.Count;

    /// <summary>Adds a binding; returns whether it was new.</summary>
    public bool Bind(string queue, string key)
    {
        if (bindings.Contains((queue, key)))
        {
            return false;
        }

        bindings.Add((queue, key));
        return true;
    }

    /// <summary>Removes a binding; returns whether there was one.</summary>
    public bool Unbind(string queue, string key) => bindings.Remove((queue, key));

    /// <summary>Removes every binding of <paramref name="queue"/>; returns how many there were.</summary>
    public int RemoveQueue(string queue) => bindings.RemoveAll(binding => binding.Queue == queue);

    /// <summary>The queues a publish with <paramref name="routingKey"/> goes to, each once, in binding order.</summary>
    public List<string> Route(string routingKey) =>
        bindings.Where(binding => Matches(binding.Key, routingKey)).Select(binding => binding.Queue).Distinct().ToList();

    private bool Matches(string bindingKey, string routingKey) => Type switch
    {
        Amqp0ExchangeType.Fanout => true,
        Amqp0ExchangeType.Direct => bindingKey == routingKey,
        _ => Amqp0TopicKeyMatcher.Matches(bindingKey, routingKey),
    };
}
