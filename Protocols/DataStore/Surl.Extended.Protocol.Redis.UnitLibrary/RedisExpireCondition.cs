namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// <c>EXPIRE</c>'s and <c>PEXPIRE</c>'s condition (the <c>EXPIRE</c> page): <c>NX</c> sets only a
/// key without expiry, <c>XX</c> only one with, <c>GT</c> only a later expiry and <c>LT</c> only an
/// earlier one, a key without expiry counting as expiring never. An option may repeat; <c>NX</c>
/// with another, <c>GT</c> with <c>LT</c>, or an unknown option is <c>-ERR syntax error</c>
/// (ADR-0106 decision 1).
/// </summary>
[Flags]
internal enum RedisExpireCondition
{
    /// <summary>No condition: always set.</summary>
    None = 0,

    /// <summary><c>NX</c>.</summary>
    OnlyWithoutExpiry = 1,

    /// <summary><c>XX</c>.</summary>
    OnlyWithExpiry = 2,

    /// <summary><c>GT</c>.</summary>
    OnlyLater = 4,

    /// <summary><c>LT</c>.</summary>
    OnlyEarlier = 8,
}
