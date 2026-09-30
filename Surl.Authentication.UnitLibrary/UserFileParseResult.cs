namespace Surl.Authentication;

/// <summary>
/// What <see cref="UserFileParser.Parse"/> made of a <c>--user-file</c>: every account, the
/// <c>--user</c> ones first, or the first line refused.
/// </summary>
/// <param name="Accounts">
/// The <c>--user</c> accounts then the file's, in order; empty when <see cref="Failure"/> is set.
/// </param>
/// <param name="Failure">The first line refused, or <see langword="null"/> when every line was read.</param>
public sealed record UserFileParseResult(IReadOnlyList<Account> Accounts, UserFileLineFailure? Failure);
