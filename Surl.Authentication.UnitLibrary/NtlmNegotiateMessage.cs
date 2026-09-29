using System.Buffers.Binary;

namespace Surl.Authentication;

/// <summary>
/// Reads a <c>NEGOTIATE_MESSAGE</c> ([MS-NLMP] section 2.2.1.1), the first leg of NTLM: only its
/// <c>NegotiateFlags</c> matter to Surl, since the domain and workstation it may name are
/// informational.
/// </summary>
internal static class NtlmNegotiateMessage
{
    // Signature, MessageType and NegotiateFlags.
    private const int MinimumLength = NtlmMessage.PrefixLength + 4;

    /// <summary>
    /// The flags a client sent, when <paramref name="message"/> is a <c>NEGOTIATE_MESSAGE</c>.
    /// </summary>
    /// <param name="message">The decoded message.</param>
    /// <returns>The client's flags, or <see langword="null"/> when it is not a negotiate message.</returns>
    public static NtlmNegotiateFlags? ReadFlags(ReadOnlySpan<byte> message) =>
        message.Length >= MinimumLength && NtlmMessage.ReadMessageType(message) == NtlmMessage.NegotiateType
            ? (NtlmNegotiateFlags)BinaryPrimitives.ReadUInt32LittleEndian(message[NtlmMessage.PrefixLength..])
            : null;
}
