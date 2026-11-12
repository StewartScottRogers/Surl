using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// Serves one flow's CoAP messages, the one code path <c>coap</c> and <c>coaps</c> share (ADR-0097
/// decision 0): the flow's message layer, its Block1 uploads and its observers, answered by one
/// <see cref="CoapResourceAnswerer"/>. Over <c>coaps</c> the flow is the decrypted DTLS
/// association, and the peer may be authenticated. Each request is first offered to the exchange's
/// fault plan (<see cref="CoapRequestFaults"/>), whose chosen error answers it in place of the
/// resource, so it registers no observation.
/// </summary>
internal static class CoapFlowServing
{
    /// <summary>
    /// Serves <paramref name="flow"/> however it ends, then discards its Block1 uploads and their
    /// temporary files (ADR-0097 decision 4) and its observations.
    /// </summary>
    /// <param name="flow">The flow, plain or decrypted.</param>
    /// <param name="context">The exchange.</param>
    /// <param name="answerer">Answers each request.</param>
    /// <param name="contentStore">Where Block1 uploads are written.</param>
    /// <param name="observations">Every observer of the process.</param>
    /// <param name="random">Draws Message IDs and retransmission factors.</param>
    /// <param name="peerIsAuthenticated">Whether the peer is authenticated (ADR-0097 decision 6).</param>
    /// <returns>A task whose outcome is serving's: <see cref="OperationCanceledException"/> when the exchange is cancelled.</returns>
    public static Task ServeAsync(
        IDatagramFlow flow,
        ExchangeContext context,
        CoapResourceAnswerer answerer,
        ContentStore contentStore,
        CoapObservations observations,
        ICoapRandomSource random,
        bool peerIsAuthenticated) =>
        ServeFlowAsync(flow, context, answerer, contentStore, observations, random, peerIsAuthenticated).Unwrap();

    // Serves the flow however it ends, then disposes the uploads and observers, and hands serving
    // back so its outcome is the call's.
    private static async Task<Task> ServeFlowAsync(
        IDatagramFlow flow,
        ExchangeContext context,
        CoapResourceAnswerer answerer,
        ContentStore contentStore,
        CoapObservations observations,
        ICoapRandomSource random,
        bool peerIsAuthenticated)
    {
        await using var uploads = new CoapBlock1Uploads(contentStore, context.TimeProvider, answerer.PreferredSizeExponent);
        CoapFlowSession? session = null;
        await using var observers = new CoapFlowObservers(
            observations,
            context,
            (request, options, cancellationToken) => answerer.AnswerAsync(request, options.ResolveLocalProxy(context.ListenUrl), context.Scheme, peerIsAuthenticated, uploads, cancellationToken),
            options => answerer.LocationOf(options.ResolveLocalProxy(context.ListenUrl), context.Scheme),
            (what, compose, cancellationToken) => session!.SendConfirmableAsync(what, compose, cancellationToken));
        session = new CoapFlowSession(
            flow,
            context,
            async (request, options, cancellationToken) => await CoapRequestFaults.ApplyAsync(context, request, options) switch
            {
                { IsDropped: true } => null,
                { ChosenError: { } error } => error,
                _ => await observers.AnswerAsync(request, options, cancellationToken),
            },
            random);
        var serving = session.ServeAsync();
        await Task.WhenAny(serving);

        return serving;
    }
}
