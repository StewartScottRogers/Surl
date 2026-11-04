namespace Surl.Authentication;

/// <summary>
/// The first <c>--user-file</c> line that is not an account, and why (ADR-0032, section 2).
/// </summary>
/// <param name="LineNumber">The line, counted from 1.</param>
/// <param name="Refusal">Why the line was refused.</param>
/// <param name="UserName">
/// The user name given twice for <see cref="AccountLineRefusal.UserNameGivenTwice"/>;
/// otherwise <see langword="null"/>.
/// </param>
public sealed record UserFileLineFailure(int LineNumber, AccountLineRefusal Refusal, string? UserName)
{
    /// <summary>
    /// The refusal as ADR-0032 section 2 words it after the path: <c>line &lt;n&gt;: &lt;reason&gt;</c>.
    /// It never holds a password. <c>Surl.Console</c> writes
    /// <c>surl: (2) User file &lt;path&gt;, </c> before it.
    /// </summary>
    /// <returns>For example <c>line 3: the password is empty</c>.</returns>
    public string Describe() => $"line {LineNumber}: {DescribeRefusal()}";

    private string DescribeRefusal() => Refusal switch
    {
        AccountLineRefusal.ExpectedUserColonPassword => "expected <user:password>",
        AccountLineRefusal.EmptyPassword => "the password is empty",
        AccountLineRefusal.ControlCharacterInUserName => "the user name holds a control character",
        AccountLineRefusal.UserNameGivenTwice => $"user {UserName} is given twice",
        _ => "not UTF-8",
    };
}
