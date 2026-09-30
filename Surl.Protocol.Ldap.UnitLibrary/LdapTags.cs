using System.Formats.Asn1;

namespace Surl.Protocol.Ldap;

/// <summary>
/// The tags of RFC 4511 appendix B's ASN.1 that are not universal, named for what they tag.
/// </summary>
internal static class LdapTags
{
    /// <summary><c>BindRequest</c>, [APPLICATION 0].</summary>
    public static readonly Asn1Tag BindRequest = new(TagClass.Application, 0, isConstructed: true);

    /// <summary><c>BindResponse</c>, [APPLICATION 1].</summary>
    public static readonly Asn1Tag BindResponse = new(TagClass.Application, 1, isConstructed: true);

    /// <summary><c>UnbindRequest</c>, [APPLICATION 2], a <c>NULL</c>.</summary>
    public static readonly Asn1Tag UnbindRequest = new(TagClass.Application, 2);

    /// <summary><c>SearchRequest</c>, [APPLICATION 3].</summary>
    public static readonly Asn1Tag SearchRequest = new(TagClass.Application, 3, isConstructed: true);

    /// <summary><c>SearchResultEntry</c>, [APPLICATION 4].</summary>
    public static readonly Asn1Tag SearchResultEntry = new(TagClass.Application, 4, isConstructed: true);

    /// <summary><c>SearchResultDone</c>, [APPLICATION 5].</summary>
    public static readonly Asn1Tag SearchResultDone = new(TagClass.Application, 5, isConstructed: true);

    /// <summary><c>AbandonRequest</c>, [APPLICATION 16], a <c>MessageID</c>.</summary>
    public static readonly Asn1Tag AbandonRequest = new(TagClass.Application, 16);

    /// <summary><c>SearchResultReference</c>, [APPLICATION 19].</summary>
    public static readonly Asn1Tag SearchResultReference = new(TagClass.Application, 19, isConstructed: true);

    /// <summary><c>ExtendedRequest</c>, [APPLICATION 23].</summary>
    public static readonly Asn1Tag ExtendedRequest = new(TagClass.Application, 23, isConstructed: true);

    /// <summary><c>ExtendedResponse</c>, [APPLICATION 24].</summary>
    public static readonly Asn1Tag ExtendedResponse = new(TagClass.Application, 24, isConstructed: true);

    /// <summary><c>controls</c> of an <c>LDAPMessage</c>, [0].</summary>
    public static readonly Asn1Tag Controls = Context(0, isConstructed: true);

    /// <summary><c>referral</c> of an <c>LDAPResult</c>, [3].</summary>
    public static readonly Asn1Tag Referral = Context(3, isConstructed: true);

    /// <summary><c>serverSaslCreds</c> of a <c>BindResponse</c>, [7].</summary>
    public static readonly Asn1Tag ServerSaslCredentials = Context(7);

    /// <summary><c>responseName</c> of an <c>ExtendedResponse</c>, [10].</summary>
    public static readonly Asn1Tag ResponseName = Context(10);

    /// <summary><c>responseValue</c> of an <c>ExtendedResponse</c>, [11].</summary>
    public static readonly Asn1Tag ResponseValue = Context(11);

    /// <summary>
    /// A context-specific tag.
    /// </summary>
    /// <param name="number">The tag number.</param>
    /// <param name="isConstructed">Whether the element is constructed.</param>
    /// <returns>The tag.</returns>
    public static Asn1Tag Context(int number, bool isConstructed = false) => new(TagClass.ContextSpecific, number, isConstructed);
}
