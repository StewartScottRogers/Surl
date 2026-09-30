namespace Surl.Authentication;

/// <summary>
/// What a server reads of the client's <c>NegTokenInit</c> (RFC 4178 section 4.2.1).
/// </summary>
/// <param name="MechTypes">The <c>mechTypes</c> object identifiers, most preferred first.</param>
/// <param name="MechToken">The optimistic <c>mechToken</c> for the first mechanism, or <see langword="null"/>.</param>
internal sealed record SpnegoNegTokenInit(IReadOnlyList<string> MechTypes, byte[]? MechToken);
