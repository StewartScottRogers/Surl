using Surl.Cryptography.Rc4;

namespace Surl.Authentication;

/// <summary>
/// One connection's NTLM handshake over decoded messages (ADR-0039), whichever scheme carries
/// them: HTTP <c>NTLM</c> directly (<see cref="NtlmConnectionVerifier"/>) or inside
/// <c>Negotiate</c> (<see cref="NegotiateConnectionVerifier"/>, ADR-0040), or SASL
/// (<see cref="NtlmSaslExchange"/>). A <c>NEGOTIATE_MESSAGE</c> is answered with a
/// <c>CHALLENGE_MESSAGE</c> over a new server challenge, which the handshake keeps; any later
/// message uses that challenge up, whatever the verdict, so an <c>AUTHENTICATE_MESSAGE</c> is
/// checked once and only against the challenge this handshake issued. Only NTLMv2 answers are
/// checked; anything else is refused, never thrown. A handshake that grants a security layer
/// (LDAP, ADR-0072 decision 4) grants signing, sealing and key exchange in its challenge and,
/// on acceptance, exports the session key ([MS-NLMP] section 3.2.5.1.2).
/// </summary>
internal sealed class NtlmHandshake(
    AccountBook accounts, INtlmServerChallengeSource serverChallenges, bool grantsSecurityLayer = false)
{
    private static readonly NtlmHandshakeStep Refused = new(HttpCredentialOutcome.Refused, null, null, null);

    // An NTLMv1 response is exactly 24 bytes; NTLMv2's is NTProofStr and a longer blob.
    private const int NtlmV1ResponseLength = 24;

    // The EncryptedRandomSessionKey of NTLMv2 with NTLMSSP_NEGOTIATE_KEY_EXCH is 16 bytes.
    private const int SessionKeyLength = 16;

    private byte[]? serverChallenge;

    /// <summary>
    /// Answers one decoded message.
    /// </summary>
    /// <param name="message">The NTLM message, as the client sent it.</param>
    /// <returns>The <c>CHALLENGE_MESSAGE</c> to send, accepted as the account, or refused.</returns>
    public NtlmHandshakeStep Answer(byte[] message)
    {
        var challenge = serverChallenge;
        serverChallenge = null;

        return NtlmMessage.ReadMessageType(message) switch
        {
            NtlmMessage.NegotiateType => AnswerNegotiate(message),
            NtlmMessage.AuthenticateType => CheckAuthenticate(message, challenge),
            _ => Refused,
        };
    }

    /// <summary>
    /// Uses up any challenge issued, as a message the carrying scheme refused before it reached
    /// <see cref="Answer"/> must.
    /// </summary>
    public void Forget() => serverChallenge = null;

    private NtlmHandshakeStep AnswerNegotiate(byte[] message)
    {
        if (NtlmNegotiateMessage.ReadFlags(message) is not { } clientFlags)
        {
            return Refused;
        }

        serverChallenge = serverChallenges.CreateServerChallenge();

        return new NtlmHandshakeStep(
            HttpCredentialOutcome.Continue,
            null,
            NtlmChallengeMessage.Create(clientFlags, serverChallenge, grantsSecurityLayer),
            null);
    }

    private NtlmHandshakeStep CheckAuthenticate(byte[] message, byte[]? challenge)
    {
        var answer = NtlmAuthenticateMessage.TryRead(message);
        if (answer is null)
        {
            return Refused;
        }

        var match = challenge is not null && answer.NtChallengeResponse.Length > NtlmV1ResponseLength
            ? FindAnsweringAccount(answer, challenge)
            : null;
        if (match is null || !TryExportSessionKey(answer, match.Value.SessionBaseKey, out var sessionKey))
        {
            return Refused with { UserAsSent = answer.UserName };
        }

        return new NtlmHandshakeStep(HttpCredentialOutcome.Accepted, match.Value.AccountName, null, answer.UserName, sessionKey);
    }

    private (string AccountName, byte[] SessionBaseKey)? FindAnsweringAccount(NtlmAuthenticateMessage answer, byte[] challenge)
    {
        var account = accounts.FindNtlmAccount(answer.UserName);
        byte[]? sessionBaseKey = null;
        foreach (var ntHash in account.NtHashes)
        {
            // Every hash is checked, so the work never says which one matched.
            var (proves, baseKey) = ProveNtHash(ntHash, answer, challenge);
            sessionBaseKey = proves ? baseKey : sessionBaseKey;
        }

        // Only an account's own hashes can match: the dummy's are random.
        return sessionBaseKey is null ? null : (account.AccountName!, sessionBaseKey);
    }

    private (bool Proves, byte[] SessionBaseKey) ProveNtHash(byte[] ntHash, NtlmAuthenticateMessage answer, byte[] challenge)
    {
        var response = answer.NtChallengeResponse.AsSpan();
        var responseKey = NtlmV2Calculation.ComputeResponseKeyNt(ntHash, answer.UserName, answer.DomainName);
        var proof = response[..NtlmV2Calculation.NtProofLength];
        var expectedProof = NtlmV2Calculation.ComputeNtProof(
            responseKey, challenge, response[NtlmV2Calculation.NtProofLength..]);

        return (accounts.SecretComparer.FixedTimeEquals(expectedProof, proof),
            NtlmV2Calculation.ComputeSessionBaseKey(responseKey, proof));
    }

    // NTLMv2's KeyExchangeKey is its SessionBaseKey ([MS-NLMP] section 3.4.5.1); with key exchange
    // the client chose the ExportedSessionKey and sent it RC4-encrypted under that key.
    private bool TryExportSessionKey(NtlmAuthenticateMessage answer, byte[] keyExchangeKey, out NtlmSessionKey? sessionKey)
    {
        sessionKey = null;
        if (!grantsSecurityLayer)
        {
            return true;
        }

        if (!answer.NegotiateFlags.HasFlag(NtlmNegotiateFlags.KeyExchange))
        {
            sessionKey = new NtlmSessionKey(keyExchangeKey, answer.NegotiateFlags);
            return true;
        }

        if (answer.EncryptedRandomSessionKey.Length != SessionKeyLength)
        {
            return false;
        }

        var exportedSessionKey = new byte[SessionKeyLength];
        new Rc4(keyExchangeKey, 0).ApplyKeyStream(answer.EncryptedRandomSessionKey, exportedSessionKey);
        sessionKey = new NtlmSessionKey(exportedSessionKey, answer.NegotiateFlags);

        return true;
    }
}
