namespace Surl.Authentication;

/// <summary>
/// What an <c>XOAUTH2</c> or <c>OAUTHBEARER</c> response carries (ADR-0049, section 5).
/// </summary>
/// <param name="User">
/// The user sent (<c>user=</c>, or <c>OAUTHBEARER</c>'s <c>a=</c>), empty when none was: an accepted
/// login's account name, the mailbox owner the session acts as (ADR-0050, decision 2).
/// </param>
/// <param name="Token">The bearer token.</param>
internal readonly record struct BearerLogin(string User, string Token);
