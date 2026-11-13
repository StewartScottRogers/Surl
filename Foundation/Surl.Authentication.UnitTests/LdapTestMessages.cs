using System.Formats.Asn1;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// Directory answers written here from RFC 4511 section 4, independently of the library's reader.
/// </summary>
internal static class LdapTestMessages
{
    public static byte[] BindResponse(int id, int code) => Result(id, 1, code);

    public static byte[] SearchDone(int id, int code) => Result(id, 5, code);

    public static byte[] ExtendedResponse(int id, int code) => Result(id, 24, code);

    public static byte[] SearchEntry(int id, string dn) =>
        Message(id, writer =>
        {
            using var entry = writer.PushSequence(new Asn1Tag(TagClass.Application, 4, true));
            writer.WriteOctetString(Encoding.UTF8.GetBytes(dn));
            using var attributes = writer.PushSequence();
        });

    public static byte[] SearchReference(int id) =>
        Message(id, writer =>
        {
            using var reference = writer.PushSequence(new Asn1Tag(TagClass.Application, 19, true));
            writer.WriteOctetString("ldap://elsewhere/"u8);
        });

    public static byte[] Message(int id, Action<AsnWriter> writeOperation)
    {
        var writer = new AsnWriter(AsnEncodingRules.BER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(id);
            writeOperation(writer);
        }

        return writer.Encode();
    }

    private static byte[] Result(int id, int applicationNumber, int code) =>
        Message(id, writer =>
        {
            using var result = writer.PushSequence(new Asn1Tag(TagClass.Application, applicationNumber, true));
            writer.WriteEnumeratedValue((LdapResultCode)code);
            writer.WriteOctetString([]);
            writer.WriteOctetString([]);
        });
}
