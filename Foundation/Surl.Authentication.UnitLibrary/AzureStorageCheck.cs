namespace Surl.Authentication;

/// <summary>
/// What <see cref="AzureStorageVerifier"/> made of a request's credentials.
/// </summary>
/// <param name="LoginMethod">
/// The method checked, as a login note names it (<c>Bearer</c>, <c>SharedKey</c>,
/// <c>SharedKeyLite</c>, <c>SAS</c>), or <see langword="null"/> when nothing could be checked: an
/// <c>Authorization</c> of another scheme, or credentials that do not read.
/// </param>
/// <param name="Check">The answer, with the account accepted or the refusal's reason.</param>
internal sealed record AzureStorageCheck(string? LoginMethod, HttpCredentialCheck Check);
