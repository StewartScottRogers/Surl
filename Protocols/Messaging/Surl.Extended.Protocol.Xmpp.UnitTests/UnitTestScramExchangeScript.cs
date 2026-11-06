using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// The server side of one SCRAM example (RFC 5802 section 5, RFC 7677 section 3) for the account
/// <c>user</c>/<c>pencil</c>, as a <see cref="UnitTestXmppSaslPolicy.Answer"/>: the example's
/// client-first message is answered with the example's server-first message, and a client-final
/// message is checked as RFC 5802 section 3 says - ClientKey XOR ClientSignature - and accepted
/// with <c>v=&lt;ServerSignature&gt;</c> as additional success data, or refused.
/// </summary>
internal sealed class UnitTestScramExchangeScript
{
    public const string Password = "pencil";

    private readonly string mechanism;
    private readonly HashAlgorithmName hash;
    private readonly string clientFirst;
    private readonly string serverFirst;

    private UnitTestScramExchangeScript(string mechanism, HashAlgorithmName hash, string clientFirst, string serverFirst)
    {
        this.mechanism = mechanism;
        this.hash = hash;
        this.clientFirst = clientFirst;
        this.serverFirst = serverFirst;
    }

    // RFC 5802 section 5.
    public static UnitTestScramExchangeScript Sha1 { get; } = new(
        "SCRAM-SHA-1",
        HashAlgorithmName.SHA1,
        "n,,n=user,r=fyko+d2lbbFgONRv9qkxdawL",
        "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096");

    // RFC 7677 section 3.
    public static UnitTestScramExchangeScript Sha256 { get; } = new(
        "SCRAM-SHA-256",
        HashAlgorithmName.SHA256,
        "n,,n=user,r=rOprNGfwEbeRWgbNEkqO",
        "r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096");

    public SaslLoginStep Answer(byte[]? message)
    {
        var text = message is null ? null : Encoding.UTF8.GetString(message);
        if (text == clientFirst)
        {
            return new SaslLoginStep(SaslLoginOutcome.Challenge, Encoding.UTF8.GetBytes(serverFirst), null, null);
        }

        var proofAt = text?.LastIndexOf(",p=", StringComparison.Ordinal) ?? -1;
        return proofAt < 0 ? Refused() : Check(text![..proofAt], text[(proofAt + 3)..]);
    }

    private SaslLoginStep Check(string withoutProof, string proof)
    {
        var parts = serverFirst.Split(',');
        var salt = Convert.FromBase64String(parts[1][2..]);
        var saltedPassword = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(Password), salt, 4096, hash, HashLength());
        var clientKey = Hmac(saltedPassword, "Client Key"u8.ToArray());
        var storedKey = CryptographicOperations.HashData(hash, clientKey);
        var authMessage = Encoding.UTF8.GetBytes($"{clientFirst[3..]},{serverFirst},{withoutProof}");
        var clientSignature = Hmac(storedKey, authMessage);
        var expectedProof = clientKey.Zip(clientSignature, (key, signature) => (byte)(key ^ signature)).ToArray();
        if (proof != Convert.ToBase64String(expectedProof))
        {
            return Refused();
        }

        var serverSignature = Hmac(Hmac(saltedPassword, "Server Key"u8.ToArray()), authMessage);
        return new SaslLoginStep(
            SaslLoginOutcome.Accepted,
            ReadOnlyMemory<byte>.Empty,
            "user",
            new CheckedLogin(mechanism, "user", true),
            AdditionalSuccessData: Encoding.UTF8.GetBytes("v=" + Convert.ToBase64String(serverSignature)));
    }

    private SaslLoginStep Refused() =>
        new(SaslLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, new CheckedLogin(mechanism, "user", false), "SCRAM: invalid-proof");

    private int HashLength() => hash == HashAlgorithmName.SHA1 ? 20 : 32;

    private byte[] Hmac(byte[] key, byte[] data) => CryptographicOperations.HmacData(hash, key, data);
}
