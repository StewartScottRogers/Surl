using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Where a <see cref="ComposedServing"/> writes while it serves (ADR-0099, decision 2); the host
/// opens them, so the composition never opens a file for writing.
/// </summary>
/// <param name="Log">The start-up warnings and notes, each written at the command line's level.</param>
/// <param name="StatusOutput">The status lines, written from the info level up (ADR-0033, section 1).</param>
/// <param name="ExchangeLogFactory">The exchange log every connection and flow is logged through.</param>
/// <param name="AllListenersBound">
/// Called once, when the last listener has bound, with each listen URL and its bound port in the
/// order the listeners started; never called when a listener cannot bind.
/// </param>
public sealed record SurlServingOutputs(
    TextWriter Log,
    TextWriter StatusOutput,
    IExchangeLogFactory ExchangeLogFactory,
    Action<IReadOnlyList<ListenUrl>> AllListenersBound);
