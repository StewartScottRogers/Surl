namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// The exchanges one <see cref="Amqp0Broker"/> holds, shared by its connections (ADR-0112 decisions
/// 0 and 5): <c>amq.direct</c>, <c>amq.fanout</c>, <c>amq.topic</c>, <c>amq.headers</c> and
/// <c>amq.match</c> from start, and those clients declare, with their bindings. The default exchange
/// is not here: it routes by queue name. Once loaded from <see cref="Amqp0ExchangeFiles"/>, its
/// durable declared exchanges and their bindings are written back by <see cref="SaveChangesAsync"/>.
/// </summary>
internal sealed class Amqp0ExchangeTable
{
    /// <summary>The most exchanges clients may declare on one server: 1000 (decision 8).</summary>
    public const int MaxDeclaredExchanges = 1000;

    /// <summary>The most bindings on the broker's own exchanges: 100000 (decision 8).</summary>
    public const int MaxBindings = 100_000;

    /// <summary>The topic exchange that is the bus's topic space.</summary>
    public const string TopicSpace = "amq.topic";

    private static readonly (string Name, Amqp0ExchangeType Type)[] Predeclared =
    [
        ("amq.direct", Amqp0ExchangeType.Direct),
        ("amq.fanout", Amqp0ExchangeType.Fanout),
        (TopicSpace, Amqp0ExchangeType.Topic),
        ("amq.headers", Amqp0ExchangeType.Headers),
        ("amq.match", Amqp0ExchangeType.Headers),
    ];

    private readonly Lock gate = new();
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private readonly Dictionary<string, Amqp0Exchange> exchanges = new(StringComparer.Ordinal);
    private readonly int maxDeclaredExchanges;
    private readonly int maxBindings;
    private int bindingCount;
    private Amqp0ExchangeFiles? files;

    // Counts changes to what the files keep; a save writes when it passed what was saved.
    private long version;
    private long savedVersion;

    /// <summary>Creates the table with the pre-declared exchanges and the given bounds.</summary>
    public Amqp0ExchangeTable(int maxDeclaredExchanges = MaxDeclaredExchanges, int maxBindings = MaxBindings)
    {
        this.maxDeclaredExchanges = maxDeclaredExchanges;
        this.maxBindings = maxBindings;
        foreach (var (name, type) in Predeclared)
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

            var declared = new Amqp0Exchange(name, type, durable, arguments, isPredeclared: false);
            exchanges.Add(name, declared);
            NoteChange(declared);
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
            NoteChange(exchange);
            return true;
        }
    }

    /// <summary>Binds a queue; returns <see langword="false"/> only when <see cref="MaxBindings"/> are held.</summary>
    public bool Bind(Amqp0Exchange exchange, Amqp0Binding binding)
    {
        lock (gate)
        {
            if (bindingCount >= maxBindings)
            {
                return false;
            }

            if (exchange.Bind(binding))
            {
                bindingCount++;
                NoteChange(exchange);
            }

            return true;
        }
    }

    /// <summary>Removes a binding; one that is not there is no error.</summary>
    public void Unbind(Amqp0Exchange exchange, Amqp0Binding binding)
    {
        lock (gate)
        {
            if (exchange.Unbind(binding))
            {
                bindingCount--;
                NoteChange(exchange);
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
                var removed = exchange.RemoveQueue(queue);
                bindingCount -= removed;
                if (removed > 0)
                {
                    NoteChange(exchange);
                }
            }
        }
    }

    /// <summary>The queues <paramref name="exchange"/> routes a publish with <paramref name="routingKey"/> and <paramref name="headers"/> to.</summary>
    public List<string> Route(Amqp0Exchange exchange, string routingKey, Amqp0FieldTable? headers)
    {
        lock (gate)
        {
            return exchange.Route(routingKey, headers);
        }
    }

    /// <summary>
    /// Loads the durable declared exchanges and their bindings <paramref name="exchangeFiles"/> hold
    /// and keeps the files, so <see cref="SaveChangesAsync"/> writes every later change to them.
    /// Called once, before any connection is served.
    /// </summary>
    /// <exception cref="InvalidOperationException">The table was loaded before, or holds a declared exchange.</exception>
    /// <exception cref="Amqp0ExchangeLoadException">The file cannot be read, does not parse, or passes a bound; nothing is taken from it.</exception>
    public async Task LoadAsync(Amqp0ExchangeFiles exchangeFiles, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (files is not null || exchanges.Count != Predeclared.Length)
            {
                throw new InvalidOperationException("An exchange table is loaded once, before any exchange is declared.");
            }
        }

        var persisted = await exchangeFiles.ReadAsync(maxDeclaredExchanges, maxBindings, cancellationToken);
        lock (gate)
        {
            foreach (var stored in persisted)
            {
                var exchange = new Amqp0Exchange(stored.Name, stored.Type, durable: true, stored.Arguments, isPredeclared: false);
                foreach (var binding in stored.Bindings)
                {
                    exchange.Bind(binding);
                }

                bindingCount += exchange.BindingCount;
                exchanges.Add(stored.Name, exchange);
            }

            files = exchangeFiles;
        }
    }

    /// <summary>
    /// Writes the durable declared exchanges and their bindings to the files when they changed since
    /// the last save. Does nothing for a table never loaded. Saves are serialised.
    /// </summary>
    /// <returns>The failed write's message, which the caller notes, or <see langword="null"/>. After a
    /// failure the change stays in memory and the next save writes it.</returns>
    public async Task<string?> SaveChangesAsync()
    {
        if (files is not { } exchangeFiles)
        {
            return null;
        }

        await saveLock.WaitAsync();
        try
        {
            long snapshotVersion;
            byte[] bytes;
            lock (gate)
            {
                if (version == savedVersion)
                {
                    return null;
                }

                snapshotVersion = version;
                bytes = Amqp0ExchangeFiles.Encode(exchanges.Values
                    .Where(exchange => exchange.IsPersisted)
                    .Select(exchange => new Amqp0PersistedExchange(exchange.Name, exchange.Type, exchange.Arguments, [.. exchange.Bindings])));
            }

            var failure = await exchangeFiles.WriteAsync(bytes);
            if (failure is null)
            {
                savedVersion = snapshotVersion;
            }

            return failure;
        }
        finally
        {
            saveLock.Release();
        }
    }

    private void NoteChange(Amqp0Exchange exchange)
    {
        if (exchange.IsPersisted)
        {
            version++;
        }
    }
}
