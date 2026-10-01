using System.Formats.Asn1;
using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Reads back what the server wrote as one line per <c>LDAPMessage</c>, so a test can say what
/// was answered: <c>#2 searchResultEntry cn=alice,dc=example,dc=com: cn=alice; mail=alice@example.com</c>,
/// <c>#2 searchResultDone success</c>, <c>#0 extendedResponse protocolError "unknown operation" name 1.3.6.1.4.1.1466.20036</c>.
/// </summary>
internal static class LdapResponseTranscript
{
    private static readonly Dictionary<int, string> OperationNames = new()
    {
        [1] = "bindResponse",
        [5] = "searchResultDone",
        [7] = "modifyResponse",
        [9] = "addResponse",
        [11] = "delResponse",
        [13] = "modDNResponse",
        [15] = "compareResponse",
        [24] = "extendedResponse",
    };

    public static IReadOnlyList<string> Of(byte[] written)
    {
        var lines = new List<string>();
        var reader = new AsnReader(written, AsnEncodingRules.BER);
        while (reader.HasData)
        {
            var message = reader.ReadSequence();
            var messageId = (int)message.ReadInteger();
            lines.Add($"#{messageId} {Describe(message)}");
            message.ThrowIfNotEmpty();
        }

        return lines;
    }

    private static string Describe(AsnReader message)
    {
        var tag = message.PeekTag();
        var operation = message.ReadSequence(tag);
        return tag.TagValue == 4 ? DescribeEntry(operation) : DescribeResult(OperationNames[tag.TagValue], operation);
    }

    private static string DescribeEntry(AsnReader entry)
    {
        var dn = Text(entry.ReadOctetString());
        var attributes = new List<string>();
        var list = entry.ReadSequence();
        while (list.HasData)
        {
            var attribute = list.ReadSequence();
            var type = Text(attribute.ReadOctetString());
            var values = attribute.ReadSetOf();
            var texts = new List<string>();
            while (values.HasData)
            {
                texts.Add(Text(values.ReadOctetString()));
            }

            attributes.Add($"{type}={string.Join(',', texts)}");
        }

        return $"searchResultEntry {dn}: {string.Join("; ", attributes)}";
    }

    private static string DescribeResult(string operationName, AsnReader result)
    {
        var code = result.ReadEnumeratedValue<LdapResultCode>();
        var matched = Text(result.ReadOctetString());
        var diagnostic = Text(result.ReadOctetString());
        var line = new StringBuilder($"{operationName} {LdapLogText.NameOf(code)}");
        if (matched.Length > 0)
        {
            line.Append($" matched {matched}");
        }

        if (diagnostic.Length > 0)
        {
            line.Append($" \"{diagnostic}\"");
        }

        while (result.HasData)
        {
            var tag = result.PeekTag();
            line.Append($" [{tag.TagValue}] {Text(result.ReadOctetString(tag))}");
        }

        return line.ToString();
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
