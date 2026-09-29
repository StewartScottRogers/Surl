using System.Buffers.Binary;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// Builds the <c>CHALLENGE_MESSAGE</c> ([MS-NLMP] section 2.2.1.2) Surl answers a
/// <c>NEGOTIATE_MESSAGE</c> with (ADR-0039): no <c>Version</c>, the target name <c>SURL</c>, and
/// target information naming <c>SURL</c> as the NetBIOS domain and computer, with no timestamp.
/// Carrying target information is what makes a client answer with NTLMv2.
/// </summary>
internal static class NtlmChallengeMessage
{
    /// <summary>
    /// The name the challenge gives the server, as its target name and in its target information:
    /// fixed, so it says nothing about the host (ADR-0006 section 3).
    /// </summary>
    public const string TargetName = "SURL";

    /// <summary>
    /// The length of a server challenge.
    /// </summary>
    public const int ServerChallengeLength = 8;

    // Signature, MessageType, TargetNameFields, NegotiateFlags, ServerChallenge, Reserved and
    // TargetInfoFields; the payload follows at once, since no Version is sent.
    private const int HeaderLength = 48;

    private const NtlmNegotiateFlags AlwaysSet =
        NtlmNegotiateFlags.RequestTarget
        | NtlmNegotiateFlags.Ntlm
        | NtlmNegotiateFlags.AlwaysSign
        | NtlmNegotiateFlags.TargetTypeServer
        | NtlmNegotiateFlags.TargetInfo;

    private const NtlmNegotiateFlags GrantedWhenAsked =
        NtlmNegotiateFlags.ExtendedSessionSecurity | NtlmNegotiateFlags.Negotiate128 | NtlmNegotiateFlags.Negotiate56;

    // MsvAvNbDomainName (2) and MsvAvNbComputerName (1), each SURL in UTF-16LE, then MsvAvEOL.
    private static readonly byte[] TargetInfo = CreateTargetInfo();

    /// <summary>
    /// The flags the challenge sets for a client that sent <paramref name="clientFlags"/>: the
    /// fixed set, UTF-16LE strings when the client offered them and OEM ones otherwise, and
    /// extended session security, 128-bit and 56-bit only when the client asked.
    /// </summary>
    /// <param name="clientFlags">The <c>NEGOTIATE_MESSAGE</c>'s flags.</param>
    /// <returns>The <c>CHALLENGE_MESSAGE</c>'s flags.</returns>
    public static NtlmNegotiateFlags ChooseFlags(NtlmNegotiateFlags clientFlags) =>
        AlwaysSet
        | (clientFlags & GrantedWhenAsked)
        | (clientFlags.HasFlag(NtlmNegotiateFlags.Unicode) ? NtlmNegotiateFlags.Unicode : NtlmNegotiateFlags.Oem);

    /// <summary>
    /// The challenge for a client that sent <paramref name="clientFlags"/>, carrying
    /// <paramref name="serverChallenge"/>.
    /// </summary>
    /// <param name="clientFlags">The <c>NEGOTIATE_MESSAGE</c>'s flags.</param>
    /// <param name="serverChallenge">The eight random bytes the client's answer must cover.</param>
    /// <returns>The message's bytes.</returns>
    public static byte[] Create(NtlmNegotiateFlags clientFlags, ReadOnlySpan<byte> serverChallenge)
    {
        var flags = ChooseFlags(clientFlags);
        var targetName = (flags.HasFlag(NtlmNegotiateFlags.Unicode) ? Encoding.Unicode : Encoding.ASCII)
            .GetBytes(TargetName);
        var message = new byte[HeaderLength + targetName.Length + TargetInfo.Length];
        var span = message.AsSpan();

        NtlmMessage.Signature.CopyTo(span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], NtlmMessage.ChallengeType);
        NtlmMessage.WriteField(span[12..], targetName.Length, HeaderLength);
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], (uint)flags);
        serverChallenge[..ServerChallengeLength].CopyTo(span[24..]);
        NtlmMessage.WriteField(span[40..], TargetInfo.Length, HeaderLength + targetName.Length);
        targetName.CopyTo(span[HeaderLength..]);
        TargetInfo.CopyTo(span[(HeaderLength + targetName.Length)..]);

        return message;
    }

    private static byte[] CreateTargetInfo()
    {
        var name = Encoding.Unicode.GetBytes(TargetName);
        var pairs = new List<byte>();
        foreach (var avId in new byte[] { 2, 1 })
        {
            // AvId and AvLen, each a 16-bit little-endian value below 256.
            pairs.AddRange([avId, 0, (byte)name.Length, 0]);
            pairs.AddRange(name);
        }

        pairs.AddRange([0, 0, 0, 0]);

        return [.. pairs];
    }
}
