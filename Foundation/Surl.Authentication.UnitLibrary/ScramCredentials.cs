using System.Text;

namespace Surl.Authentication;

/// <summary>
/// What a SCRAM server keeps of one account under one <see cref="ScramHash"/> (RFC 5802 section
/// 3): the salt and iteration count it sends, and <c>StoredKey</c> and <c>ServerKey</c>, from which
/// it checks a proof and signs its answer. The password itself is not needed after they are derived.
/// </summary>
/// <param name="AccountName">The account's name, or <see langword="null"/> for the dummy an unknown user gets.</param>
/// <param name="Salt">The salt.</param>
/// <param name="IterationCount">The iteration count.</param>
/// <param name="StoredKey"><c>H(ClientKey)</c>.</param>
/// <param name="ServerKey"><c>HMAC(SaltedPassword, "Server Key")</c>.</param>
internal sealed record ScramCredentials(
    string? AccountName, byte[] Salt, int IterationCount, byte[] StoredKey, byte[] ServerKey)
{
    /// <summary>
    /// The iteration count every account gets: 4096, RFC 5802's and RFC 7677's example and the
    /// minimum RFC 7677 section 4 recommends.
    /// </summary>
    public const int DefaultIterationCount = 4096;

    /// <summary>
    /// The length of the random salt each account gets under each hash.
    /// </summary>
    public const int SaltLength = 16;

    /// <summary>
    /// Derives an account's credentials: <c>SaltedPassword := Hi(Normalize(password), salt, i)</c>,
    /// <c>ClientKey := HMAC(SaltedPassword, "Client Key")</c>, <c>StoredKey := H(ClientKey)</c> and
    /// <c>ServerKey := HMAC(SaltedPassword, "Server Key")</c>, with <see cref="SaslPrep"/> as Normalize.
    /// </summary>
    /// <param name="hash">The mechanism's hash.</param>
    /// <param name="accountName">The account's name.</param>
    /// <param name="password">The account's password.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="iterationCount">The iteration count.</param>
    /// <returns>The credentials.</returns>
    public static ScramCredentials Derive(
        ScramHash hash, string accountName, string password, byte[] salt, int iterationCount)
    {
        var saltedPassword = hash.Hi(Encoding.UTF8.GetBytes(SaslPrep.Normalize(password)), salt, iterationCount);
        var clientKey = hash.Hmac(saltedPassword, "Client Key"u8);

        return new ScramCredentials(
            accountName, salt, iterationCount, hash.Hash(clientKey), hash.Hmac(saltedPassword, "Server Key"u8));
    }
}
