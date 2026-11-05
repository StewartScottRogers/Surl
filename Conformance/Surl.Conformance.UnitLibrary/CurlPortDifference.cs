namespace Surl.Conformance;

/// <summary>
/// One compared item on which the Curl port's run differs from upstream curl's, after
/// normalisation (ADR-0089 decision 6).
/// </summary>
/// <param name="Item">The item, e.g. <c>exit code</c>, <c>standard output</c> or <c>stored file a.txt</c>.</param>
/// <param name="Upstream">Upstream's side: its exit code, or the first differing line or byte and where it is.</param>
/// <param name="Port">The port's side, in the same form.</param>
public sealed record CurlPortDifference(string Item, string Upstream, string Port)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Item}: upstream {Upstream}; port {Port}";
}
