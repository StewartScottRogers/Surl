namespace Surl.Cli;

/// <summary>
/// One <c>--topic-access &lt;filter&gt;=&lt;member&gt;:&lt;pub|sub|pubsub&gt;,...</c> (ADR-0101 decision 4, ADR-0102
/// decision 3.4): who may publish (<c>pub</c>), subscribe (<c>sub</c>) or both (<c>pubsub</c>) on the topics a
/// filter matches. The record holds what was written, percent-decoded; <c>Surl.Composition</c> expands groups
/// and warns about <c>@everyone</c> (BL-991).
/// </summary>
/// <param name="Filter">The canonical topic filter, never empty, never holding a control character.</param>
/// <param name="Grants">The grants as written, in order, each access <c>pub</c>, <c>sub</c> or <c>pubsub</c>; no member twice.</param>
public sealed record ConfiguredTopicAccess(string Filter, IReadOnlyList<ConfiguredGrant> Grants);
