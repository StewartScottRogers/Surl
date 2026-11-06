namespace Surl.Conformance;

/// <summary>
/// How one Phase 7 case ended (ADR-0089 decision 5): its <see cref="CurlPortVerdictKind"/>, the
/// <see cref="CurlPortFinding"/> of a port defect, and the reason of a Surl defect or of a case that
/// is not comparable.
/// </summary>
public sealed class CurlPortVerdict
{
    private CurlPortVerdict(CurlPortVerdictKind kind, CurlPortFinding? finding, string reason)
    {
        Kind = kind;
        Finding = finding;
        Reason = reason;
    }

    /// <summary>
    /// Gets the verdict.
    /// </summary>
    public CurlPortVerdictKind Kind { get; }

    /// <summary>
    /// Gets the finding of a <see cref="CurlPortVerdictKind.PortDefect"/>, otherwise <see langword="null"/>.
    /// </summary>
    public CurlPortFinding? Finding { get; }

    /// <summary>
    /// Gets what upstream's run did that the mirrored test does not assert (a Surl defect), the named
    /// reason a case is not comparable, or an empty string for the other verdicts.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets the verdict that every compared item agrees.
    /// </summary>
    /// <returns>An agreement.</returns>
    public static CurlPortVerdict Agreement() => new(CurlPortVerdictKind.Agreement, null, string.Empty);

    /// <summary>
    /// Gets the verdict that the port's run differs from upstream's.
    /// </summary>
    /// <param name="finding">The finding.</param>
    /// <returns>A port defect.</returns>
    public static CurlPortVerdict PortDefect(CurlPortFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return new(CurlPortVerdictKind.PortDefect, finding, string.Empty);
    }

    /// <summary>
    /// Gets the verdict that upstream's own run is not what the mirrored test asserts.
    /// </summary>
    /// <param name="reason">What differs from the mirrored test's assertions.</param>
    /// <returns>A Surl defect.</returns>
    public static CurlPortVerdict SurlDefect(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(CurlPortVerdictKind.SurlDefect, null, reason);
    }

    /// <summary>
    /// Gets the verdict that the runs differ for a named reason that is not the port's.
    /// </summary>
    /// <param name="reason">The named reason, e.g. <c>release difference: …</c>.</param>
    /// <returns>A case that is not comparable.</returns>
    public static CurlPortVerdict NotComparable(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(CurlPortVerdictKind.NotComparable, null, reason);
    }

    /// <inheritdoc/>
    public override string ToString() => Kind switch
    {
        CurlPortVerdictKind.Agreement => "agreement",
        CurlPortVerdictKind.PortDefect => $"port defect\n{Finding}",
        CurlPortVerdictKind.SurlDefect => $"Surl defect: {Reason}",
        _ => $"not comparable: {Reason}",
    };
}
