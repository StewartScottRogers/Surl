namespace Surl.Authentication;

/// <summary>
/// Ends one <see cref="LdapAccountSource"/> check as a refusal, carrying the reason the verbose log
/// gives the operator (ADR-0149, decision 9). The message is that reason alone: never a password,
/// a DN or the URL.
/// </summary>
internal sealed class LdapCheckRefusedException : Exception
{
    private LdapCheckRefusedException(string reason)
        : base(reason)
    {
    }

    /// <summary>The reason, as decision 9 words it.</summary>
    public string Reason => Message;

    /// <summary>A refusal for <paramref name="reason"/>.</summary>
    /// <param name="reason">The operator-facing reason.</param>
    /// <returns>The exception.</returns>
    public static LdapCheckRefusedException Because(string reason) => new(reason);

    /// <summary>The directory's answer could not be read, or it ended mid-message.</summary>
    /// <returns>The exception.</returns>
    public static LdapCheckRefusedException MalformedAnswer() => new("malformed answer");
}
