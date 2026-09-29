using System.Threading.Channels;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

/// <summary>
/// What a <see cref="FakeConnectionProtocolServer"/> was given for one exchange.
/// </summary>
internal sealed record ServedExchange(IConnection Connection, ExchangeContext Context);

/// <summary>
/// An <see cref="IConnectionProtocolServer"/> that records every exchange it is given and
/// answers it with the behaviour a test scripted.
/// </summary>
internal sealed class FakeConnectionProtocolServer(
    Func<IConnection, ExchangeContext, Task> serve, params string[] schemes) : IConnectionProtocolServer
{
    private readonly Channel<ServedExchange> exchanges = Channel.CreateUnbounded<ServedExchange>();

    public FakeConnectionProtocolServer(params string[] schemes)
        : this((_, _) => Task.CompletedTask, schemes)
    {
    }

    public IReadOnlyList<string> Schemes { get; } = schemes;

    /// <summary>
    /// Waits for the next exchange the server was given.
    /// </summary>
    public Task<ServedExchange> NextExchangeAsync() =>
        exchanges.Reader.ReadAsync().AsTask().WaitAsync(Patience.Timeout);

    public bool TryTakeExchange(out ServedExchange? exchange) => exchanges.Reader.TryRead(out exchange);

    public async Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        exchanges.Writer.TryWrite(new ServedExchange(connection, context));

        await serve(connection, context);
    }
}

/// <summary>
/// What a <see cref="FakeDatagramProtocolServer"/> was given for one exchange.
/// </summary>
internal sealed record ServedFlow(IDatagramFlow Flow, ExchangeContext Context);

/// <summary>
/// An <see cref="IDatagramProtocolServer"/> that records every flow it is given and answers
/// it with the behaviour a test scripted.
/// </summary>
internal class FakeDatagramProtocolServer(
    Func<IDatagramFlow, ExchangeContext, Task> serve, params string[] schemes) : IDatagramProtocolServer
{
    private readonly Channel<ServedFlow> flows = Channel.CreateUnbounded<ServedFlow>();

    public FakeDatagramProtocolServer(params string[] schemes)
        : this((_, _) => Task.CompletedTask, schemes)
    {
    }

    public IReadOnlyList<string> Schemes { get; } = schemes;

    /// <summary>
    /// Waits for the next flow the server was given.
    /// </summary>
    public Task<ServedFlow> NextFlowAsync() =>
        flows.Reader.ReadAsync().AsTask().WaitAsync(Patience.Timeout);

    public bool TryTakeFlow(out ServedFlow? flow) => flows.Reader.TryRead(out flow);

    public async Task ServeAsync(IDatagramFlow flow, ExchangeContext context)
    {
        flows.Writer.TryWrite(new ServedFlow(flow, context));

        await serve(flow, context);
    }
}

/// <summary>
/// A <see cref="FakeDatagramProtocolServer"/> that also writes refusals: it records the
/// refusal it is asked to write and sends <c>refused:&lt;refusal&gt;</c>, unless the test
/// scripted other behaviour.
/// </summary>
internal sealed class FakeRefusalWritingDatagramProtocolServer(
    Func<IDatagramFlow, ExchangeContext, Task> serve,
    Func<IDatagramFlow, ConnectionRefusal, CancellationToken, Task>? writeRefusal,
    params string[] schemes)
    : FakeDatagramProtocolServer(serve, schemes), IDatagramRefusalWriter
{
    private readonly Channel<ConnectionRefusal> refusals = Channel.CreateUnbounded<ConnectionRefusal>();

    /// <summary>
    /// Waits for the next refusal the server was asked to write.
    /// </summary>
    public Task<ConnectionRefusal> NextRefusalAsync() =>
        refusals.Reader.ReadAsync().AsTask().WaitAsync(Patience.Timeout);

    public async ValueTask WriteRefusalAsync(IDatagramFlow flow, ConnectionRefusal refusal, CancellationToken cancellationToken)
    {
        refusals.Writer.TryWrite(refusal);

        if (writeRefusal is null)
        {
            await flow.SendAsync(System.Text.Encoding.ASCII.GetBytes($"refused:{refusal}"), cancellationToken);

            return;
        }

        await writeRefusal(flow, refusal, cancellationToken);
    }
}

/// <summary>
/// A protocol server that claims to answer both connections and datagram flows, which
/// ADR-0004 forbids.
/// </summary>
internal sealed class FakeTwoTransportProtocolServer(params string[] schemes) : IConnectionProtocolServer, IDatagramProtocolServer
{
    public IReadOnlyList<string> Schemes { get; } = schemes;

    public Task ServeAsync(IConnection connection, ExchangeContext context) => Task.CompletedTask;

    public Task ServeAsync(IDatagramFlow flow, ExchangeContext context) => Task.CompletedTask;
}

/// <summary>
/// A protocol server that answers neither connections nor datagram flows.
/// </summary>
internal sealed class FakeTransportlessProtocolServer(params string[] schemes) : IProtocolServer
{
    public IReadOnlyList<string> Schemes { get; } = schemes;
}
