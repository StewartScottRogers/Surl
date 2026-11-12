using System.Formats.Asn1;
using System.Numerics;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// The few <c>LDAPMessage</c>s <see cref="LdapAccountSource"/> sends and reads, in BER on
/// <c>System.Formats.Asn1</c> (RFC 4511; ADR-0149, decisions 4 and 11).
/// </summary>
internal static class LdapMessageCodec
{
    /// <summary>The largest message read, by its BER length (ADR-0149, decision 10).</summary>
    public const int MaxMessageBytes = 64 * 1024;

    /// <summary>The <c>requestName</c> of StartTLS (RFC 4511 section 4.14.1).</summary>
    public const string StartTlsOid = "1.3.6.1.4.1.1466.20037";

    private static Asn1Tag Application(int number, bool isConstructed) =>
        new(TagClass.Application, number, isConstructed);

    private static Asn1Tag Context(int number) => new(TagClass.ContextSpecific, number, false);

    /// <summary>A simple <c>BindRequest</c>, LDAP version 3 (RFC 4511 section 4.2).</summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="name">The DN, or empty for the anonymous identity.</param>
    /// <param name="password">The password bytes.</param>
    /// <returns>The encoded message.</returns>
    public static byte[] BindRequest(int messageId, string name, byte[] password) =>
        Message(messageId, writer =>
        {
            using var request = writer.PushSequence(Application(0, true));
            writer.WriteInteger(3);
            writer.WriteOctetString(Encoding.UTF8.GetBytes(name));
            writer.WriteOctetString(password, Context(0));
        });

    /// <summary>The StartTLS <c>ExtendedRequest</c> (RFC 4511 section 4.14.1).</summary>
    /// <param name="messageId">The message ID.</param>
    /// <returns>The encoded message.</returns>
    public static byte[] StartTlsRequest(int messageId) =>
        Message(messageId, writer =>
        {
            using var request = writer.PushSequence(Application(23, true));
            writer.WriteOctetString(Encoding.ASCII.GetBytes(StartTlsOid), Context(0));
        });

    /// <summary>
    /// The <c>SearchRequest</c> for one user (ADR-0149, decision 4, step 3): the URL's base and scope,
    /// no aliases followed, two entries at most, no attributes (<c>1.1</c>), and the URL's filter
    /// and the user name as an equality match. The name is a value, never filter syntax.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="url">The directory's URL.</param>
    /// <param name="userName">The user name, matched as <see cref="LdapDirectoryUrl.Attribute"/>'s value.</param>
    /// <param name="timeLimitSeconds">The server-side time limit.</param>
    /// <returns>The encoded message.</returns>
    public static byte[] SearchRequest(int messageId, LdapDirectoryUrl url, string userName, int timeLimitSeconds) =>
        Message(messageId, writer =>
        {
            using var request = writer.PushSequence(Application(3, true));
            writer.WriteOctetString(Encoding.UTF8.GetBytes(url.BaseDn));
            writer.WriteEnumeratedValue(url.Scope);
            writer.WriteEnumeratedValue(LdapAliasDereferencing.Never);
            writer.WriteInteger(2);
            writer.WriteInteger(timeLimitSeconds);
            writer.WriteBoolean(true);
            using (writer.PushSetOf(new Asn1Tag(TagClass.ContextSpecific, 0, true)))
            {
                writer.WriteEncodedValue(url.EncodedFilter);
                using var equality = writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 3, true));
                writer.WriteOctetString(Encoding.UTF8.GetBytes(url.Attribute));
                writer.WriteOctetString(Encoding.UTF8.GetBytes(userName));
            }

            using var attributes = writer.PushSequence();
            writer.WriteOctetString("1.1"u8);
        });

    /// <summary>The <c>UnbindRequest</c> (RFC 4511 section 4.3).</summary>
    /// <param name="messageId">The message ID.</param>
    /// <returns>The encoded message.</returns>
    public static byte[] UnbindRequest(int messageId) =>
        Message(messageId, writer => writer.WriteNull(Application(2, false)));

    /// <summary>
    /// Reads one message from <paramref name="stream"/>, refusing a length above <see cref="MaxMessageBytes"/>
    /// before reading the body.
    /// </summary>
    /// <param name="stream">The connection.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The message.</returns>
    /// <exception cref="LdapCheckRefusedException">The message is not one this class reads.</exception>
    /// <exception cref="EndOfStreamException">The connection ended inside the message.</exception>
    public static async ValueTask<LdapResponse> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var head = new byte[2];
        await stream.ReadExactlyAsync(head, cancellationToken).ConfigureAwait(false);
        var length = head[1] & 0x7F;
        var extra = new byte[3];
        var lengthBytes = head[1] < 0x80 ? 0 : length;
        if (head[0] != 0x30 || lengthBytes > 3 || head[1] == 0x80)
        {
            throw LdapCheckRefusedException.MalformedAnswer();
        }

        await stream.ReadExactlyAsync(extra.AsMemory(0, lengthBytes), cancellationToken).ConfigureAwait(false);
        length = lengthBytes == 0 ? length : extra.Take(lengthBytes).Aggregate(0, (total, next) => (total << 8) | next);
        if (length > MaxMessageBytes)
        {
            throw LdapCheckRefusedException.MalformedAnswer();
        }

        var body = new byte[length];
        await stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);

        return Parse(body);
    }

    private static LdapResponse Parse(byte[] body)
    {
        try
        {
            var reader = new AsnReader(body, AsnEncodingRules.BER);
            var id = (int)reader.ReadInteger();
            var tag = reader.PeekTag();
            if (tag.TagClass != TagClass.Application)
            {
                throw LdapCheckRefusedException.MalformedAnswer();
            }

            var operation = reader.ReadSequence(tag);
            var kind = KindOf(tag.TagValue);
            var isEntry = kind == LdapResponseKind.SearchEntry;
            var code = kind is LdapResponseKind.SearchReference || isEntry ? 0 : ReadResultCode(operation);
            var name = isEntry ? Encoding.UTF8.GetString(operation.ReadOctetString()) : null;

            return new LdapResponse(id, kind, code, name);
        }
        catch (Exception exception) when (exception is AsnContentException or OverflowException)
        {
            throw LdapCheckRefusedException.MalformedAnswer();
        }
    }

    private static int ReadResultCode(AsnReader operation) =>
        (int)operation.ReadEnumeratedValue<LdapResultCode>();

    private static LdapResponseKind KindOf(int applicationNumber) => applicationNumber switch
    {
        1 => LdapResponseKind.Bind,
        4 => LdapResponseKind.SearchEntry,
        5 => LdapResponseKind.SearchDone,
        19 => LdapResponseKind.SearchReference,
        24 => LdapResponseKind.Extended,
        _ => throw LdapCheckRefusedException.MalformedAnswer(),
    };

    private static byte[] Message(int messageId, Action<AsnWriter> writeOperation)
    {
        var writer = new AsnWriter(AsnEncodingRules.BER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(new BigInteger(messageId));
            writeOperation(writer);
        }

        return writer.Encode();
    }
}
