namespace Surl.Authentication;

/// <summary>
/// What a server reads of the client's <c>NegTokenInit</c> (RFC 4178 section 4.2.1).
/// </summary>
/// <param name="MechTypes">The <c>mechTypes</c> object identifiers, most preferred first.</param>
/// <param name="MechToken">The optimistic <c>mechToken</c> for the first mechanism, or <see langword="null"/>.</param>
/// <param name="MechTypesDer">
/// The DER of the <c>MechTypeList</c> as the client sent it, which a <c>mechListMIC</c> is taken over
/// (RFC 4178 section 5).
/// </param>
/// <param name="MechListMic">The client's <c>mechListMIC</c>, or <see langword="null"/> when it sent none.</param>
internal sealed record SpnegoNegTokenInit(
    IReadOnlyList<string> MechTypes, byte[]? MechToken, byte[] MechTypesDer, byte[]? MechListMic);
