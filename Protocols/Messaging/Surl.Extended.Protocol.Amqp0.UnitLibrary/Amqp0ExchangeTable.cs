namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The exchanges one <see cref="Amqp0Broker"/> holds, shared by its connections (ADR-0112 decisions
/// 0 and 5): <c>amq.direct</c>, <c>amq.fanout</c> and <c>amq.topic</c> from start, and those clients
/// declare, with their bindings. The default exchange is not here: it routes by queue name.
/// </summary>
internal sealed class Amqp0ExchangeTable
{
    /// <summary>The most exchanges clients may declare on one server: 1000 (decision 8).</summary>
    public const int MaxDeclaredExchanges = 1000;

    /// <summary>The most bindings on the broker's own exchanges: 100000 (decision 8).</summary>
    public const int MaxBindings = 100_000;

    /// <summary>The topic exchange that is the bus's topic space.</summary>
    public const string TopicSpace = "amq.topic";

    private readonly Lock gate = new();
    private readonly Dictionary<string, Amqp0Exchange> exchanges = new(StringComparer.Ordinal);
    private readonly int maxDeclaredExchanges;
    private readonly int maxBindings;
    private int bindingCount;

    /// <summary>Creates the table with the pre-declared exchanges and the given bounds.</summary>
    public Amqp0ExchangeTable(int maxDeclaredExchanges = MaxDeclaredExchanges, int maxBindings = MaxBindings)
    {
        this.maxDeclaredExchanges = maxDeclaredExchanges;
        this.maxBindings = maxBindings;
        foreach (var (name, type) in new[] { ("amq.direct", Amqp0ExchangeType.Direct), ("amq.fanout", Amqp0ExchangeType.Fanout), (TopicSpace, Amqp0ExchangeType.Topic) })
        {
            exchanges.Add(name, new(name, type, durable: true, new byte[4], isPredeclared: true));
        }
    }

    /// <summary>The exchange named <paramref name="name"/>, or <see langword="null"/>.</summary>
    public Amqp0Exchange? Find(string name)
    {
        lock (gate)
        {
            return exchanges.GetValueOrDefault(name);
        }
    }

    /// <summary>Makes the exchange, or checks an existing one is the same.</summary>
    public Amqp0ExchangeDeclareOutcome Declare(string name, Amqp0ExchangeType type, bool durable, byte[] arguments)
    {
        lock (gate)
        {
            if (exchanges.TryGetValue(name, out var existing))
            {
                return existing.Type != type ? Amqp0ExchangeDeclareOutcome.TypeDiffers
                    : existing.Durable != durable || !existing.Arguments.AsSpan().SequenceEqual(arguments) ? Amqp0ExchangeDeclareOutcome.FlagsDiffer
                    : Amqp0ExchangeDeclareOutcome.Declared;
            }

            if (exchanges.Values.Count(exchange => !exchange.IsPredeclared) >= maxDeclaredExchanges)
            {
                return Amqp0ExchangeDeclareOutcome.TooManyExchanges;
            }

            exchanges.Add(name, new(name, type, durable, arguments, isPredeclared: false));
            return Amqp0ExchangeDeclareOutcome.Declared;
        }
    }

    /// <summary>Deletes a declared exchange with its bindings, unless <paramref name="ifUnused"/> finds one.</summary>
    /// <returns>Whether it was deleted.</returns>
    public bool Delete(Amqp0Exchange exchange, bool ifUnused)
    {
        lock (gate)
        {
            if (ifUnused && exchange.BindingCount > 0)
            {
                return false;
            }

            bindingCount -= exchange.BindingCount;
            exchanges.Remove(exchange.Name);
            return true;
        }
    }

    /// <summary>Binds a queue; returns <see langword="false"/> only when <see cref="MaxBindings"/> are held.</summary>
    public bool Bind(Amqp0Exchange exchange, string queue, string key)
    {
        lock (gate)
        {
            if (bindingCount >= maxBindings)
            {
                return false;
            }

            if (exchange.Bind(queue, key))
            {
                bindingCount++;
            }

            return true;
        }
    }

    /// <summary>Removes a binding; one that is not there is no error.</summary>
    public void Unbind(Amqp0Exchange exchange, string queue, string key)
    {
        lock (gate)
        {
            if (exchange.Unbind(queue, key))
            {
                bindingCount--;
            }
        }
    }

    /// <summary>Removes every binding of a deleted queue.</summary>
    public void RemoveQueue(string queue)
    {
        lock (gate)
        {
            foreach (var exchange in exchanges.Values)
            {
                bindingCount -= exchange.RemoveQueue(queue);
            }
        }
    }

    /// <summary>The queues <paramref name="exchange"/> routes <paramref name="routingKey"/> to.</summary>
    public List<string> Route(Amqp0Exchange exchange, string routingKey)
    {
        lock (gate)
        {
            return exchange.Route(routingKey);
        }
    }
}
