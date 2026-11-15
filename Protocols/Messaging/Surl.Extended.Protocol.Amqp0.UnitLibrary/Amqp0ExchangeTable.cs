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
            return Amqp0ExchangeDeclareOutcome.Created;
        }
    }

    /// <summary>Deletes a declared exchange with its bindings, unless <paramref name="ifUnused"/> finds one.</summary>
    /// <param name="exchange">The declared exchange to delete.</param>
    /// <param name="ifUnused">Whether to refuse when the exchange holds a binding.</param>
    /// <param name="removedBindings">The bindings the deletion took with the exchange, in binding order.</param>
    /// <returns>Whether it was deleted.</returns>
    public bool Delete(Amqp0Exchange exchange, bool ifUnused, out List<Amqp0Binding> removedBindings)
    {
        lock (gate)
        {
            removedBindings = [];
            if (ifUnused && exchange.BindingCount > 0)
            {
                return false;
            }

            removedBindings.AddRange(exchange.Bindings);
            bindingCount -= exchange.BindingCount;
            exchanges.Remove(exchange.Name);
            NoteChange(exchange);
            return true;
        }
    }

    /// <summary>Binds a queue; returns <see langword="false"/> only when <see cref="MaxBindings"/> are held.</summary>
    /// <param name="exchange">The exchange to bind on.</param>
    /// <param name="binding">The binding to add.</param>
    /// <param name="added">Whether the table did not hold the binding before.</param>
    public bool Bind(Amqp0Exchange exchange, Amqp0Binding binding, out bool added)
    {
        lock (gate)
        {
            added = false;
            if (bindingCount >= maxBindings)
            {
                return false;
            }

            added = exchange.Bind(binding);
            if (added)
            {
                bindingCount++;
                NoteChange(exchange);
            }

            return true;
        }
    }

    /// <summary>Removes a binding; one that is not there is no error.</summary>
    /// <returns>Whether the table held the binding.</returns>
    public bool Unbind(Amqp0Exchange exchange, Amqp0Binding binding)
    {
        lock (gate)
        {
            var removed = exchange.Unbind(binding);
            if (removed)
            {
                bindingCount--;
                NoteChange(exchange);
            }

            return removed;
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

    /// <summary>Whether the table keeps its exchanges in files, so reset, snapshot and restore refuse (ADR-0159 decision 4).</summary>
    public bool IsOnDisk
    {
        get
        {
            lock (gate)
            {
                return files is not null;
            }
        }
    }

    /// <summary>
    /// Removes every declared exchange and every binding, returning the table to the pre-declared exchanges.
    /// </summary>
    /// <exception cref="InvalidOperationException">The table keeps its exchanges in files.</exception>
    public void Reset()
    {
        lock (gate)
        {
            RefuseWhenOnDisk();
            Replace(Predeclared.Select(item => new Amqp0ExchangeCopy(item.Name, item.Type, true, new byte[4], true, [])));
        }
    }

    /// <summary>Takes a deep copy of the exchanges and their bindings.</summary>
    /// <exception cref="InvalidOperationException">The table keeps its exchanges in files.</exception>
    public Amqp0ExchangeSnapshot TakeSnapshot()
    {
        lock (gate)
        {
            RefuseWhenOnDisk();
            return new(this, [.. exchanges.Values.Select(exchange =>
                new Amqp0ExchangeCopy(exchange.Name, exchange.Type, exchange.Durable, exchange.Arguments, exchange.IsPredeclared, [.. exchange.Bindings]))]);
        }
    }

    /// <summary>Replaces the exchanges and bindings with those of <paramref name="snapshot"/>.</summary>
    /// <exception cref="InvalidOperationException">The table keeps its exchanges in files, or the snapshot is another table's.</exception>
    public void Restore(Amqp0ExchangeSnapshot snapshot)
    {
        lock (gate)
        {
            RefuseWhenOnDisk();
            if (!ReferenceEquals(snapshot.Owner, this))
            {
                throw new InvalidOperationException("The snapshot was taken by another AMQP 0-9-1 broker.");
            }

            Replace(snapshot.Exchanges);
        }
    }

    private void RefuseWhenOnDisk()
    {
        if (files is not null)
        {
            throw new InvalidOperationException("The AMQP 0-9-1 exchanges are kept in files under --directory and are not reset, snapshotted or restored.");
        }
    }

    private void Replace(IEnumerable<Amqp0ExchangeCopy> copies)
    {
        exchanges.Clear();
        bindingCount = 0;
        foreach (var copy in copies)
        {
            var exchange = copy.ToExchange();
            exchanges.Add(copy.Name, exchange);
            bindingCount += exchange.BindingCount;
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
