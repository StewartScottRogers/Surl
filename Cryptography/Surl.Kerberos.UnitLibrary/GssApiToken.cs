using System.Formats.Asn1;

namespace Surl.Kerberos;

/// <summary>
/// The GSS-API framing of the Kerberos context tokens (RFC 1964 section 1.1, RFC 4121 section 4.1,
/// RFC 2743 section 3.1): <c>[APPLICATION 0]</c> around the mechanism OID, a two-byte
/// <c>TOK_ID</c> and the Kerberos message, which is not itself inside the OID's DER.
/// </summary>
internal static class GssApiToken
{
    /// <summary>The Kerberos V5 mechanism OID of RFC 1964.</summary>
    public const string KerberosOid = "1.2.840.113554.1.2.2";

    /// <summary>Microsoft's Kerberos OID, which Windows lists in SPNEGO (MS-SPNG section 3.3.5.1).</summary>
    public const string MicrosoftKerberosOid = "1.2.840.48018.1.2.2";

    /// <summary>The <c>TOK_ID</c> of a <c>KRB_AP_REQ</c>.</summary>
    public static ReadOnlySpan<byte> ApRequestTokenId => [0x01, 0x00];

    /// <summary>The <c>TOK_ID</c> of a <c>KRB_AP_REP</c>.</summary>
    public static ReadOnlySpan<byte> ApReplyTokenId => [0x02, 0x00];

    private static readonly Asn1Tag InitialContextTokenTag = KerberosDer.ApplicationTag(0);

    /// <summary>
    /// Reads a token that must be exactly one framed Kerberos message with
    /// <paramref name="tokenId" />, and returns the message.
    /// </summary>
    /// <param name="token">The whole token.</param>
    /// <param name="tokenId">The <c>TOK_ID</c> required.</param>
    /// <param name="acceptsMicrosoftKerberosOid">Whether <see cref="MicrosoftKerberosOid" /> is accepted too: only inside SPNEGO.</param>
    /// <returns>The Kerberos message after the <c>TOK_ID</c>.</returns>
    /// <exception cref="AsnContentException">The token is not so framed.</exception>
    public static byte[] ReadMessage(ReadOnlySpan<byte> token, ReadOnlySpan<byte> tokenId, bool acceptsMicrosoftKerberosOid)
    {
        ReadOnlySpan<byte> content = ReadInitialContextTokenContent(token);
        string oid = AsnDecoder.ReadObjectIdentifier(content, KerberosDer.Rules, out int oidLength);
        if (oid != KerberosOid && !(acceptsMicrosoftKerberosOid && oid == MicrosoftKerberosOid))
        {
            throw new AsnContentException("The token's mechanism is not Kerberos.");
        }

        ReadOnlySpan<byte> innerToken = content[oidLength..];
        if (!innerToken.StartsWith(tokenId))
        {
            throw new AsnContentException("The token's TOK_ID is not the one expected.");
        }

        return innerToken[tokenId.Length..].ToArray();
    }

    private static ReadOnlySpan<byte> ReadInitialContextTokenContent(ReadOnlySpan<byte> token)
    {
        Asn1Tag tag = Asn1Tag.Decode(token, out _);
        AsnDecoder.ReadEncodedValue(token, KerberosDer.Rules, out int contentOffset, out int contentLength, out int consumed);
        if (tag != InitialContextTokenTag || consumed != token.Length)
        {
            throw new AsnContentException("The token is not one [APPLICATION 0].");
        }

        return token.Slice(contentOffset, contentLength);
    }

    /// <summary>Frames <paramref name="message" /> under the Kerberos OID and <paramref name="tokenId" />.</summary>
    /// <param name="tokenId">The <c>TOK_ID</c>.</param>
    /// <param name="message">The Kerberos message's DER.</param>
    /// <returns>The token.</returns>
    public static byte[] Frame(ReadOnlySpan<byte> tokenId, ReadOnlySpan<byte> message)
    {
        AsnWriter oidWriter = new(KerberosDer.Rules);
        oidWriter.WriteObjectIdentifier(KerberosOid);
        byte[] content = [.. oidWriter.Encode(), .. tokenId, .. message];

        // The content is not a run of DER values, so it is written as the primitive
        // [APPLICATION 0], whose identifier octet 0x40 differs from the constructed 0x60 in the
        // constructed bit alone; the writer still encodes the length.
        AsnWriter tokenWriter = new(KerberosDer.Rules);
        tokenWriter.WriteOctetString(content, new Asn1Tag(TagClass.Application, 0));
        byte[] token = tokenWriter.Encode();
        token[0] = 0x60;
        return token;
    }
}
