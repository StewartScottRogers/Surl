namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// How long a DNS over HTTPS answer may be cached, as its <c>Cache-Control: max-age</c> says
/// (RFC 8484 section 5.1; ADR-0092 decision 5).
/// </summary>
internal static class DnsOverHttpsCacheLifetime
{
    /// <summary>
    /// Returns the smallest TTL in <paramref name="message"/>'s answer section; with no answer,
    /// the TTL of the SOA in its authority section, which a negative answer carries at its negative
    /// TTL (RFC 2308 section 5); with neither - an error, a refusal or a referral - 0.
    /// </summary>
    public static uint MaxAgeOf(DnsMessage message)
    {
        if (message.Answers.Count > 0)
        {
            return message.Answers.Min(record => record.TimeToLive);
        }

        return message.Authorities.FirstOrDefault(record => record.Type == DnsRecordType.SOA)?.TimeToLive ?? 0;
    }
}
