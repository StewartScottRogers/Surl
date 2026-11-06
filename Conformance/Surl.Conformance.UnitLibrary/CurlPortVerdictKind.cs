namespace Surl.Conformance;

/// <summary>
/// The verdicts a Phase 7 case can end in (ADR-0089 decision 5). A case missing a pinned file is
/// Inconclusive, not a verdict.
/// </summary>
public enum CurlPortVerdictKind
{
    /// <summary>
    /// Every compared item is equal after normalisation.
    /// </summary>
    Agreement,

    /// <summary>
    /// Upstream's run is what the mirrored test asserts, and the port's run differs in a compared item.
    /// </summary>
    PortDefect,

    /// <summary>
    /// Upstream's own run is not what the mirrored test asserts; the port's run is not judged.
    /// </summary>
    SurlDefect,

    /// <summary>
    /// The runs differ for a named reason that is not the port's, e.g. <c>release difference</c>.
    /// </summary>
    NotComparable,
}
