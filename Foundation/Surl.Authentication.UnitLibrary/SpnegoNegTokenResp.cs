namespace Surl.Authentication;

/// <summary>
/// What a server reads of a client's later token, a <c>negTokenResp</c> (RFC 4178 section 4.2.2).
/// </summary>
/// <param name="ResponseToken">The <c>responseToken</c>, empty when the client sent none.</param>
/// <param name="MechListMic">The client's <c>mechListMIC</c>, or <see langword="null"/> when it sent none.</param>
internal sealed record SpnegoNegTokenResp(byte[] ResponseToken, byte[]? MechListMic);
