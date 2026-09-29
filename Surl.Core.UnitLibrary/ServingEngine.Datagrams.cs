using System.Net;
using Surl.Protocol.Abstractions;

namespace Surl.Core;

// Datagram flows: the listen URLs whose scheme belongs to an IDatagramProtocolServer (ADR-0004,
// sections 3 and 4). A flow counts as one connection against the connection limits and is
// timed like one (ADR-0006, sections 1 and 5); a flow has no Abort, so a failed exchange's
// flow is only disposed.
public sealed partial class ServingEngine
{
    private async Task<StartedListener> StartDatagramListenerAsync(
        ListenUrl listenUrl, IDatagramProtocolServer server, CancellationToken cancellationToken)
    {
        var listener = await listenerFactory.StartDatagramListenerAsync(listenUrl, cancellationToken);

        return new StartedListener(
            listener,
            state =>
            {
                var route = new AcceptedFlowRoute(listener.ListenUrl, server, state.Admission, state.ShutdownToken);

                return AcceptUntilStoppedAsync(token => AcceptFlowAsync(listener, route, token), state);
            });
    }

    // Accepts one flow and hands back what to run for it. A flow past a connection limit is
    // refused, uncounted, and never becomes an exchange.
    private async Task<Func<Task>> AcceptFlowAsync(
        IDatagramListener listener, AcceptedFlowRoute route, CancellationToken cancellationToken)
    {
        var flow = await listener.AcceptFlowAsync(cancellationToken);
        var remoteEndPoint = flow.RemoteEndPoint;

        return route.Admission.TryAdmit(remoteEndPoint) is { } refusal
            ? () => RefuseFlowAsync(flow, route, refusal)
            : () => RunFlowExchangeAsync(flow, remoteEndPoint, route);
    }

    // The flow gets the server's refusal, if it writes one, within the refusal deadline, and is
    // disposed whatever the writer did (ADR-0006, section 5).
    private async Task RefuseFlowAsync(IDatagramFlow flow, AcceptedFlowRoute route, ConnectionRefusal refusal)
    {
        if (route.Server is IDatagramRefusalWriter writer)
        {
            await CaptureFailureAsync(() => WriteRefusalWithinDeadlineAsync(
                token => writer.WriteRefusalAsync(flow, refusal, token), route.ShutdownToken));
        }

        await CaptureFailureAsync(() => flow.DisposeAsync().AsTask());
    }

    // Whatever the server or the log throws, the flow is disposed; then, whatever happened, it
    // stops counting against the connection limits.
    private async Task RunFlowExchangeAsync(IDatagramFlow flow, EndPoint remoteEndPoint, AcceptedFlowRoute route)
    {
        try
        {
            using var deadlines = new ExchangeDeadlines(connectionLimits, timeProvider, route.ShutdownToken);

            await CaptureFailureAsync(() => ServeAndLogFlowExchangeAsync(flow, route, deadlines));
            await CaptureFailureAsync(() => flow.DisposeAsync().AsTask());
        }
        finally
        {
            route.Admission.Release(remoteEndPoint);
        }
    }

    // The first datagram crossed the transport before the server was handed the flow, so the
    // engine logs it as received when the exchange opens (ADR-0004, section 5).
    private async Task ServeAndLogFlowExchangeAsync(IDatagramFlow flow, AcceptedFlowRoute route, ExchangeDeadlines deadlines)
    {
        var (exchangeId, log, context) = OpenExchange(route.ListenUrl, flow.LocalEndPoint, flow.RemoteEndPoint, deadlines);

        log.BytesReceived(flow.FirstDatagram.Span);

        var watchedFlow = new IdleClockRestartingDatagramFlow(new RecordingDatagramFlow(flow, log), deadlines);
        var failure = await CaptureFailureAsync(() => route.Server.ServeAsync(watchedFlow, context));

        _ = NoteHowTheExchangeEnded(failure?.SourceException, log, exchangeId, deadlines.Reason);
        log.Note($"Exchange {exchangeId} ended; closing the flow.");
    }

    // Where one datagram listener's flows go: the listen URL and server they are served under,
    // the counts they are admitted against, and the token cancelled at shutdown.
    private sealed record AcceptedFlowRoute(
        ListenUrl ListenUrl,
        IDatagramProtocolServer Server,
        ConnectionAdmission Admission,
        CancellationToken ShutdownToken);
}
