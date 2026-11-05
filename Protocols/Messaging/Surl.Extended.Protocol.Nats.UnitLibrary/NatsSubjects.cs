namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The subject rules the server checks itself (the subjects page; ADR-0113 decision 4), beside the
/// bus's mapping: a SUB subject's wildcards stand alone, and a subscription subject matching a
/// subject, <c>*</c> one token and a last <c>&gt;</c> one or more.
/// </summary>
internal static class NatsSubjects
{
    /// <summary>Whether no token holds <c>*</c> or <c>&gt;</c> inside a longer token.</summary>
    public static bool HasOnlyWholeTokenWildcards(string subject) =>
        subject.Split('.').All(token => token is "*" or ">" || token.IndexOfAny(['*', '>']) < 0);

    /// <summary>Whether a subscription subject matches a publish subject.</summary>
    public static bool Matches(string subscription, string subject)
    {
        var patternTokens = subscription.Split('.');
        var subjectTokens = subject.Split('.');
        for (var index = 0; index < patternTokens.Length; index++)
        {
            if (patternTokens[index] == ">")
            {
                return subjectTokens.Length > index;
            }

            if (index >= subjectTokens.Length || (patternTokens[index] != "*" && patternTokens[index] != subjectTokens[index]))
            {
                return false;
            }
        }

        return patternTokens.Length == subjectTokens.Length;
    }
}
