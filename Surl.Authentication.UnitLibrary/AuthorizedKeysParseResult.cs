namespace Surl.Authentication;

/// <summary>
/// What <see cref="AuthorizedKeysParser.Parse"/> made of an <c>--authorized-keys</c> file: every
/// key, in order, or the first line refused.
/// </summary>
/// <param name="Keys">The file's keys, in order; empty when <see cref="Failure"/> is set or the file holds none.</param>
/// <param name="Failure">The first line refused, or <see langword="null"/> when every line was read.</param>
public sealed record AuthorizedKeysParseResult(IReadOnlyList<AuthorizedKey> Keys, AuthorizedKeysLineFailure? Failure);
