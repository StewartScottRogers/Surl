namespace Surl.Protocol.Abstractions;

/// <summary>
/// Where a protocol server draws the bytes of a non-secret generated identifier - a generated queue
/// name, a request ID, a server ID, a container or stream ID, a message ID - that grants nothing
/// (ADR-0124, decision 6). A seeded source makes those identifiers the same every run, so a test
/// that uses Surl as a stand-in gets the same answer each time.
/// </summary>
/// <remarks>
/// Never draw anything secret from it: keys, nonces, challenges, salts, session or connection
/// tokens, cookies, TLS or SSH randoms, Kerberos material, or anything else that grants access or
/// protects a secret. Those come from <see cref="System.Security.Cryptography.RandomNumberGenerator"/>
/// directly, because a seeded source would make them predictable.
/// </remarks>
public interface IIdentifierRandom
{
    /// <summary>
    /// Fills <paramref name="destination"/> with the source's next bytes.
    /// </summary>
    /// <param name="destination">Where the bytes go; every byte of it is written.</param>
    void NextBytes(Span<byte> destination);
}
