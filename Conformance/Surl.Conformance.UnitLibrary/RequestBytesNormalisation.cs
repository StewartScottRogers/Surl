using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace Surl.Conformance;

/// <summary>
/// ADR-0089 decision 4's rules for the bytes a client sent on one connection, behind
/// <see cref="CurlPortResultNormaliser.NormaliseRequestBytes"/>: the client ports FTP's <c>PORT</c> and
/// <c>EPRT</c> name, the <c>User-Agent:</c> value, every multipart boundary, Digest's <c>nonce</c> (surl's own, echoed), <c>cnonce</c> and the
/// <c>response</c> computed from it (in a header or a base64 SASL <c>DIGEST-MD5</c> line), and the NTLM
/// client challenge, time stamp and what is derived from them (raw, as SMB carries it, or base64, as HTTP
/// and SASL carry it).
/// </summary>
internal static class RequestBytesNormalisation
{
    private const RegexOptions Lines = RegexOptions.CultureInvariant | RegexOptions.Multiline;

    private static readonly Regex FtpPortCommand = new(@"^(PORT [0-9]+,[0-9]+,[0-9]+,[0-9]+),[0-9]+,[0-9]+", Lines | RegexOptions.IgnoreCase);

    private static readonly Regex FtpEprtCommand = new(@"^(EPRT \|[0-9]\|[^|\r\n]*\|)[0-9]+\|", Lines | RegexOptions.IgnoreCase);

    private static readonly Regex UserAgentField = new(@"^(User-Agent:[ \t]*)[^\r\n]*", Lines | RegexOptions.IgnoreCase);

    private static readonly Regex Boundary = new("boundary=(\"?)([^\";\\r\\n]+)\\1", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex DigestCnonce = new("\\b(cnonce=\"?)[^\",\\r\\n]*", RegexOptions.CultureInvariant);

    // \b keeps cnonce out: no word boundary lies between its c and n.
    private static readonly Regex DigestNonce = new("\\b(nonce=\"?)[^\",\\r\\n]*", RegexOptions.CultureInvariant);

    private static readonly Regex DigestResponse =new("\\b(response=\"?)[^\",\\r\\n]*", RegexOptions.CultureInvariant);

    private static readonly Regex Base64Token = new("(?<![A-Za-z0-9+/=])[A-Za-z0-9+/]{20,}={0,2}(?![A-Za-z0-9+/=])", RegexOptions.CultureInvariant);

    private static readonly byte[] NtlmAuthenticateSignature = "NTLMSSP\0\u0003\0\0\0"u8.ToArray();

    // NTLM AUTHENTICATE_MESSAGE (MS-NLMP 2.2.1.3): the six security buffers' offsets in the message.
    private static readonly int[] NtlmSecurityBuffers = [12, 20, 28, 36, 44, 52];

    // LmChallengeResponse, NtChallengeResponse and EncryptedRandomSessionKey: what the client challenge,
    // the time stamp and the random session key decide.
    private static readonly int[] NtlmRandomFields = [12, 20, 52];

    private const int NtlmMicOffset = 72;
    private const int NtlmMicLength = 16;

    public static string Normalise(byte[] requestBytes)
    {
        var bytes = (byte[])requestBytes.Clone();
        ClearNtlmRandomness(bytes);
        var text = Encoding.Latin1.GetString(bytes);
        text = FtpPortCommand.Replace(text, "$1," + CurlPortResultNormaliser.PortPlaceholder);
        text = FtpEprtCommand.Replace(text, "${1}" + CurlPortResultNormaliser.PortPlaceholder + "|");
        text = UserAgentField.Replace(text, "${1}" + CurlPortResultNormaliser.UserAgentPlaceholder);
        text = Base64Token.Replace(text, NormaliseBase64Token);
        text = NormaliseDigest(text);
        return ReplaceBoundaries(text);
    }

    private static string NormaliseDigest(string text) =>
        DigestNonce.Replace(
            DigestResponse.Replace(
                DigestCnonce.Replace(text, "${1}" + CurlPortResultNormaliser.CnoncePlaceholder),
                "${1}" + CurlPortResultNormaliser.DigestResponsePlaceholder),
            "${1}" + CurlPortResultNormaliser.DigestNoncePlaceholder);

    private static string ReplaceBoundaries(string text)
    {
        foreach (var boundary in Boundary.Matches(text).Select(match => match.Groups[2].Value).Distinct().ToArray())
        {
            text = text.Replace(boundary, CurlPortResultNormaliser.BoundaryPlaceholder, StringComparison.Ordinal);
        }

        return text;
    }

    private static string NormaliseBase64Token(Match token)
    {
        var decoded = new byte[token.Length];
        if (!Convert.TryFromBase64String(token.Value, decoded, out var length))
        {
            return token.Value;
        }

        decoded = decoded[..length];
        if (decoded.AsSpan().StartsWith(NtlmAuthenticateSignature))
        {
            ClearNtlmRandomness(decoded);
            return Convert.ToBase64String(decoded);
        }

        var text = Encoding.Latin1.GetString(decoded);
        return text.Contains("cnonce=", StringComparison.Ordinal)
            ? Convert.ToBase64String(Encoding.Latin1.GetBytes(NormaliseDigest(text)))
            : token.Value;
    }

    private static void ClearNtlmRandomness(byte[] bytes)
    {
        var at = 0;
        int found;
        while ((found = bytes.AsSpan(at).IndexOf(NtlmAuthenticateSignature)) >= 0)
        {
            at += found;
            ClearNtlmAuthenticateMessage(bytes.AsSpan(at));
            at += NtlmAuthenticateSignature.Length;
        }
    }

    private static void ClearNtlmAuthenticateMessage(Span<byte> message)
    {
        if (message.Length < NtlmMicOffset)
        {
            return;
        }

        foreach (var field in NtlmRandomFields)
        {
            ClearSecurityBuffer(message, field);
        }

        if (FirstPayloadOffset(message) >= NtlmMicOffset + NtlmMicLength && message.Length >= NtlmMicOffset + NtlmMicLength)
        {
            message.Slice(NtlmMicOffset, NtlmMicLength).Clear();
        }
    }

    private static void ClearSecurityBuffer(Span<byte> message, int field)
    {
        var length = BinaryPrimitives.ReadUInt16LittleEndian(message[field..]);
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(message[(field + 4)..]);
        if (offset + (long)length <= message.Length)
        {
            message.Slice((int)offset, length).Clear();
        }
    }

    // The MIC sits between the fixed fields and the payload only when the payload starts after it
    // (MS-NLMP 2.2.1.3), which a message with a Version field and a MIC shows by its offsets.
    private static long FirstPayloadOffset(ReadOnlySpan<byte> message)
    {
        var first = long.MaxValue;
        foreach (var field in NtlmSecurityBuffers)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(message[field..]) > 0)
            {
                first = Math.Min(first, BinaryPrimitives.ReadUInt32LittleEndian(message[(field + 4)..]));
            }
        }

        return first;
    }
}
