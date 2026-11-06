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
    private readonly List<Amqp0Binding> bindings = [];

    public string Name { get; } = name;

    public Amqp0ExchangeType Type { get; } = type;

    public bool Durable { get; } = durable;

    public byte[] Arguments { get; } = arguments;

    public bool IsPredeclared { get; } = isPredeclared;

    /// <summary>Whether its declaration and bindings are kept across a restart: durable, and not pre-declared.</summary>
    public bool IsPersisted => Durable && !IsPredeclared;

    public int BindingCount => bindings.Count;

    /// <summary>Its bindings, in binding order.</summary>
    public IReadOnlyList<Amqp0Binding> Bindings => bindings;

    /// <summary>Adds a binding; returns whether it was new.</summary>
    public bool Bind(Amqp0Binding binding)
    {
        if (bindings.Exists(binding.IsSameAs))
        {
            return false;
        }

        bindings.Add(binding);
        return true;
    }

    /// <summary>Removes a binding; returns whether there was one.</summary>
    public bool Unbind(Amqp0Binding binding) => bindings.RemoveAll(binding.IsSameAs) > 0;

    /// <summary>Removes every binding of <paramref name="queue"/>; returns how many there were.</summary>
    public int RemoveQueue(string queue) => bindings.RemoveAll(binding => binding.Queue == queue);

    /// <summary>The queues a publish with <paramref name="routingKey"/> and <paramref name="headers"/> goes to, each once, in binding order.</summary>
    public List<string> Route(string routingKey, Amqp0FieldTable? headers) =>
        bindings.Where(binding => Matches(binding, routingKey, headers)).Select(binding => binding.Queue).Distinct().ToList();

    private bool Matches(Amqp0Binding binding, string routingKey, Amqp0FieldTable? headers) =>
        Type == Amqp0ExchangeType.Headers ? Amqp0HeadersMatcher.Matches(binding.Arguments, headers) : MatchesKey(binding.Key, routingKey);

    private bool MatchesKey(string bindingKey, string routingKey) => Type switch
    {
        Amqp0ExchangeType.Fanout => true,
        Amqp0ExchangeType.Direct => bindingKey == routingKey,
        _ => Amqp0TopicKeyMatcher.Matches(bindingKey, routingKey),
    };
}
