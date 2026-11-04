using System.Formats.Asn1;
using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Builds the BER of client requests with <see cref="AsnWriter"/>, straight from RFC 4511
/// appendix B, so decoder tests never take their bytes from the decoder's own encoder.
/// </summary>
internal static class LdapRequestBytes
{
    public static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    public static byte[] Message(int messageId, Action<AsnWriter> writeOperation, Action<AsnWriter>? writeControls = null)
    {
        var writer = new AsnWriter(AsnEncodingRules.BER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(messageId);
            writeOperation(writer);
            writeControls?.Invoke(writer);
        }

        return writer.Encode();
    }

    public static void Text(AsnWriter writer, string text, Asn1Tag? tag = null) => writer.WriteOctetString(Encoding.UTF8.GetBytes(text), tag);

    public static Action<AsnWriter> SimpleBind(int version, string name, string password) => writer =>
    {
        using var bind = writer.PushSequence(LdapTags.BindRequest);
        writer.WriteInteger(version);
        Text(writer, name);
        Text(writer, password, LdapTags.Context(0));
    };

    public static Action<AsnWriter> Search(
        Action<AsnWriter> filter,
        string baseObject = "dc=example,dc=com",
        int scope = 2,
        int derefAliases = 0,
        int sizeLimit = 0,
        int timeLimit = 0,
        bool typesOnly = false,
        params string[] attributes) => writer =>
    {
        using var search = writer.PushSequence(LdapTags.SearchRequest);
        Text(writer, baseObject);
        writer.WriteEnumeratedValue((LdapSearchScope)scope);
        writer.WriteEnumeratedValue((LdapDerefAliases)derefAliases);
        writer.WriteInteger(sizeLimit);
        writer.WriteInteger(timeLimit);
        writer.WriteBoolean(typesOnly);
        filter(writer);
        using var selection = writer.PushSequence();
        foreach (var attribute in attributes)
        {
            Text(writer, attribute);
        }
    };

    public static Action<AsnWriter> Comparison(int tagNumber, string attribute, string value) => writer =>
    {
        using var assertion = writer.PushSequence(LdapTags.Context(tagNumber, isConstructed: true));
        Text(writer, attribute);
        Text(writer, value);
    };

    public static Action<AsnWriter> Equality(string attribute, string value) => Comparison(3, attribute, value);

    public static Action<AsnWriter> Present(string attribute) => writer => Text(writer, attribute, LdapTags.Context(7));

    public static Action<AsnWriter> And(params Action<AsnWriter>[] filters) => Set(0, filters);

    public static Action<AsnWriter> Or(params Action<AsnWriter>[] filters) => Set(1, filters);

    public static Action<AsnWriter> Not(Action<AsnWriter> filter) => writer =>
    {
        using var not = writer.PushSequence(LdapTags.Context(2, isConstructed: true));
        filter(writer);
    };

    /// <summary>A substrings filter whose parts are written in the order given, as (context tag number, text).</summary>
    public static Action<AsnWriter> Substrings(string attribute, params (int Tag, string Text)[] parts) => writer =>
    {
        using var filter = writer.PushSequence(LdapTags.Context(4, isConstructed: true));
        Text(writer, attribute);
        using var substrings = writer.PushSequence();
        foreach (var (tag, text) in parts)
        {
            Text(writer, text, LdapTags.Context(tag));
        }
    };

    public static Action<AsnWriter> Extensible(string? matchingRule, string? type, string matchValue, bool? dnAttributes) => writer =>
    {
        using var assertion = writer.PushSequence(LdapTags.Context(9, isConstructed: true));
        if (matchingRule is not null)
        {
            Text(writer, matchingRule, LdapTags.Context(1));
        }

        if (type is not null)
        {
            Text(writer, type, LdapTags.Context(2));
        }

        Text(writer, matchValue, LdapTags.Context(3));
        if (dnAttributes is { } flag)
        {
            writer.WriteBoolean(flag, LdapTags.Context(4));
        }
    };

    public static Action<AsnWriter> Raw(string hex) => writer => writer.WriteEncodedValue(Hex(hex));

    private static Action<AsnWriter> Set(int tagNumber, Action<AsnWriter>[] filters) => writer =>
    {
        using var set = writer.PushSequence(LdapTags.Context(tagNumber, isConstructed: true));
        foreach (var filter in filters)
        {
            filter(writer);
        }
    };
}
