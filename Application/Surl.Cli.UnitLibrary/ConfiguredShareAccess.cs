namespace Surl.Cli;

/// <summary>
/// One <c>--share-access &lt;share&gt;=&lt;member&gt;:&lt;r|rw&gt;,...</c> (ADR-0101 decision 4, ADR-0102 decision 2.2):
/// who may read (<c>r</c>) or also write (<c>rw</c>) in one <c>--share</c>. The record holds what was written,
/// percent-decoded; <c>Surl.Composition</c> expands groups and <c>@everyone</c> (BL-988).
/// </summary>
/// <param name="Share">The share's name, never empty, never holding a control character.</param>
/// <param name="Grants">The grants as written, in order, each access <c>r</c> or <c>rw</c>; no member twice.</param>
public sealed record ConfiguredShareAccess(string Share, IReadOnlyList<ConfiguredGrant> Grants);
