using System.Formats.Asn1;
using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Encodes the <c>LDAPMessage</c> responses a server sends (RFC 4511, section 4 and appendix B)
/// in BER with definite lengths, as RFC 4511 section 5.1 requires.
/// </summary>
internal static class LdapMessageEncoder
{
    /// <summary>
    /// The <c>responseName</c> of the Notice of Disconnection (RFC 4511, section 4.4.1).
    /// </summary>
    public const string NoticeOfDisconnectionName = "1.3.6.1.4.1.1466.20036";

    /// <summary>
    /// Encodes a <c>BindResponse</c> (RFC 4511, section 4.2.2).
    /// </summary>
    /// <param name="messageId">The <c>messageID</c> of the bind answered.</param>
    /// <param name="result">The result.</param>
    /// <param name="serverSaslCredentials">The <c>serverSaslCreds</c>; <see langword="null"/> leaves the field out.</param>
    /// <returns>The message's encoding.</returns>
    public static byte[] EncodeBindResponse(int messageId, LdapResult result, byte[]? serverSaslCredentials) =>
        EncodeMessage(messageId, writer =>
        {
            using var response = writer.PushSequence(LdapTags.BindResponse);
            WriteResult(writer, result);
            WriteOptionalOctetString(writer, serverSaslCredentials, LdapTags.ServerSaslCredentials);
        });

    /// <summary>
    /// Encodes a <c>SearchResultEntry</c> (RFC 4511, section 4.5.2).
    /// </summary>
    /// <param name="messageId">The <c>messageID</c> of the search answered.</param>
    /// <param name="objectName">The entry's DN.</param>
    /// <param name="attributes">The entry's attributes, in the order they are sent.</param>
    /// <returns>The message's encoding.</returns>
    public static byte[] EncodeSearchResultEntry(int messageId, string objectName, IReadOnlyList<LdapPartialAttribute> attributes) =>
        EncodeMessage(messageId, writer =>
        {
            using var entry = writer.PushSequence(LdapTags.SearchResultEntry);
            WriteString(writer, objectName);
            using var attributeList = writer.PushSequence();
            foreach (var attribute in attributes)
            {
                WritePartialAttribute(writer, attribute);
            }
        });

    /// <summary>
    /// Encodes a <c>SearchResultDone</c> (RFC 4511, section 4.5.2).
    /// </summary>
    /// <param name="messageId">The <c>messageID</c> of the search answered.</param>
    /// <param name="result">The result.</param>
    /// <returns>The message's encoding.</returns>
    public static byte[] EncodeSearchResultDone(int messageId, LdapResult result) =>
        EncodeMessage(messageId, writer =>
        {
            using var done = writer.PushSequence(LdapTags.SearchResultDone);
            WriteResult(writer, result);
        });

    /// <summary>
    /// Encodes a <c>SearchResultReference</c> (RFC 4511, section 4.5.3).
    /// </summary>
    /// <param name="messageId">The <c>messageID</c> of the search answered.</param>
    /// <param name="uris">The continuation references; at least one.</param>
    /// <returns>The message's encoding.</returns>
    public static byte[] EncodeSearchResultReference(int messageId, IReadOnlyList<string> uris)
    {
        ArgumentOutOfRangeException.ThrowIfZero(uris.Count);

        return EncodeMessage(messageId, writer => WriteUris(writer, uris, LdapTags.SearchResultReference));
    }

    /// <summary>
    /// Encodes an <c>ExtendedResponse</c> (RFC 4511, section 4.12).
    /// </summary>
    /// <param name="messageId">The <c>messageID</c> of the request answered.</param>
    /// <param name="result">The result.</param>
    /// <param name="responseName">The <c>responseName</c> OID; <see langword="null"/> leaves the field out.</param>
    /// <param name="responseValue">The <c>responseValue</c>; <see langword="null"/> leaves the field out.</param>
    /// <returns>The message's encoding.</returns>
    public static byte[] EncodeExtendedResponse(int messageId, LdapResult result, string? responseName, byte[]? responseValue) =>
        EncodeMessage(messageId, writer =>
        {
            using var response = writer.PushSequence(LdapTags.ExtendedResponse);
            WriteResult(writer, result);
            WriteOptionalOctetString(writer, responseName is null ? null : Encoding.UTF8.GetBytes(responseName), LdapTags.ResponseName);
            WriteOptionalOctetString(writer, responseValue, LdapTags.ResponseValue);
        });

    /// <summary>
    /// Encodes the Notice of Disconnection (RFC 4511, section 4.4.1): an unsolicited
    /// <c>ExtendedResponse</c> with <c>messageID</c> 0, <see cref="NoticeOfDisconnectionName"/> as
    /// its name and no value, sent before the server closes the connection.
    /// </summary>
    /// <param name="result">Why the server is closing, such as <see cref="LdapResultCode.ProtocolError"/>.</param>
    /// <returns>The message's encoding.</returns>
    public static byte[] EncodeNoticeOfDisconnection(LdapResult result) =>
        EncodeExtendedResponse(0, result, NoticeOfDisconnectionName, null);

    private static byte[] EncodeMessage(int messageId, Action<AsnWriter> writeOperation)
    {
        var writer = new AsnWriter(AsnEncodingRules.BER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(messageId);
            writeOperation(writer);
        }

        return writer.Encode();
    }

    private static void WriteResult(AsnWriter writer, LdapResult result)
    {
        writer.WriteEnumeratedValue(result.ResultCode);
        WriteString(writer, result.MatchedDn);
        WriteString(writer, result.DiagnosticMessage);
        if (result.Referral is { Count: > 0 } referral)
        {
            WriteUris(writer, referral, LdapTags.Referral);
        }
    }

    private static void WriteUris(AsnWriter writer, IReadOnlyList<string> uris, Asn1Tag tag)
    {
        using var sequence = writer.PushSequence(tag);
        foreach (var uri in uris)
        {
            WriteString(writer, uri);
        }
    }

    private static void WritePartialAttribute(AsnWriter writer, LdapPartialAttribute attribute)
    {
        using var partialAttribute = writer.PushSequence();
        WriteString(writer, attribute.Type);
        using var values = writer.PushSetOf();
        foreach (var value in attribute.Values)
        {
            writer.WriteOctetString(value);
        }
    }

    private static void WriteOptionalOctetString(AsnWriter writer, byte[]? value, Asn1Tag tag)
    {
        if (value is not null)
        {
            writer.WriteOctetString(value, tag);
        }
    }

    private static void WriteString(AsnWriter writer, string text) => writer.WriteOctetString(Encoding.UTF8.GetBytes(text));
}
