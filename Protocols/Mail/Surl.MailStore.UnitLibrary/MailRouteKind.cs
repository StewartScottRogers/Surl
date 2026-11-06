namespace Surl.MailStore;

/// <summary>
/// Which kind of configured route a <see cref="MailRouteException"/> is about.
/// </summary>
public enum MailRouteKind
{
    /// <summary>A <see cref="MailAlias"/>: <c>--mail-alias</c>.</summary>
    Alias,

    /// <summary>A <see cref="MailCatchAll"/>: <c>--mail-catch-all</c>.</summary>
    CatchAll,
}
