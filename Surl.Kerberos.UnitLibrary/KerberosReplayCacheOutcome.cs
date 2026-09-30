namespace Surl.Kerberos;

/// <summary>What <see cref="KerberosReplayCache" /> did with an authenticator it was offered.</summary>
internal enum KerberosReplayCacheOutcome
{
    /// <summary>It was not held, and is now.</summary>
    Added,

    /// <summary>It was held already: a replay.</summary>
    Replayed,

    /// <summary>It was not held, and the cache holds <see cref="KerberosReplayCache.Capacity" /> others.</summary>
    Full,
}
