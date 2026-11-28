namespace Surl.MailStore;

/// <summary>
/// Why an alias or catch-all cannot be resolved (ADR-0102, decision 1.3): each is refused at
/// start. A target that stops naming an owner while the store runs is not refused: it adds no
/// recipient (ADR-0185's 2026-11-28 note).
/// </summary>
public enum MailRouteProblem
{
    /// <summary>A target is outside every configured mail domain:
    /// <c>target &lt;address&gt; is not in any --mail-domain</c>.</summary>
    TargetOutsideMailDomains,

    /// <summary>A target resolves to no owner: <c>target &lt;address&gt; names no mailbox</c>.</summary>
    TargetNamesNoMailbox,

    /// <summary>The route reaches itself through aliases or catch-alls:
    /// <c>alias &lt;address&gt; includes itself</c>.</summary>
    IncludesItself,
}
