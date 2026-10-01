namespace Surl.Protocol.Smb;

/// <summary>
/// Where the SMB server's 8-byte NTLMv1 challenge comes from: one fresh challenge per
/// connection, sent in the negotiate response and answered by the client's LM and NT responses
/// (ADR-0073, decision 1). Injected so a test can fix the challenge and replay the responses
/// upstream curl computed for it.
/// </summary>
public interface ISmbChallengeSource
{
    /// <summary>
    /// Fills <paramref name="challenge"/> with the challenge's bytes.
    /// </summary>
    /// <param name="challenge">Where the bytes go; 8 bytes long.</param>
    void Fill(Span<byte> challenge);
}
