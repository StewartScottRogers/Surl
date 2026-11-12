using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// One upstream test case's <c>&lt;servercmd&gt;</c> as the SOCKS server half plays it (ADR-0080
/// decision 2): what upstream's <c>socksd</c> reads from the case's server command file.
/// </summary>
/// <remarks>
/// <para>
/// <c>response N</c> makes every request fail with reply code <c>N</c>: SOCKS4 answers <c>5B</c>
/// whatever <c>N</c> is, and SOCKS5 answers <c>N</c> when it is <c>02</c> to <c>06</c> and
/// <c>01</c> otherwise. <c>method 2</c> makes the listener require an RFC 1929 login, and
/// <c>user</c> and <c>password</c> name the only account it accepts (without them any login is
/// accepted). <c>backendport N</c> connects every request to port <c>N</c> instead of the port the
/// request names. Every request connects to <c>127.0.0.1</c>, whatever host the request names, as
/// upstream's <c>socksd</c> does. <c>Testnum</c> is the line runtests writes first and is ignored.
/// </para>
/// <para>
/// Never compose it into the shipped <c>surl</c>; it is a test seam that
/// <c>Surl.Conformance.UnitTests</c> composes.
/// </para>
/// </remarks>
public sealed class SocksUpstreamTestCaseReplies
{
    private const string PasswordMethod = "2";

    /// <summary>
    /// Reads a case's <c>&lt;servercmd&gt;</c> lines.
    /// </summary>
    /// <param name="serverCommands">The lines.</param>
    /// <exception cref="NotSupportedException">A line names a command, or a <c>method</c> or number, the server half lacks.</exception>
    public SocksUpstreamTestCaseReplies(IEnumerable<string> serverCommands)
    {
        ArgumentNullException.ThrowIfNull(serverCommands);

        foreach (var line in serverCommands.Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')))
        {
            var words = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
            Apply(words[0], words.Length > 1 ? words[1].Trim() : string.Empty);
        }
    }

    private void Apply(string command, string value)
    {
        switch (command)
        {
            case "Testnum":
                break;
            case "response":
                Response = NumberOf(command, value);
                break;
            case "method":
                RequiresLogin = value == PasswordMethod ? true : throw Unsupported(command);
                break;
            case "user":
                User = value;
                break;
            case "password":
                Password = value;
                break;
            case "backendport":
                BackendPort = NumberOf(command, value);
                break;
            default:
                throw Unsupported(command);
        }
    }

    /// <summary>
    /// The first words of the <c>&lt;servercmd&gt;</c> lines the SOCKS server half accepts. A case
    /// using any other is skipped <c>servercmd-unsupported:&lt;command&gt;</c>.
    /// </summary>
    public static IReadOnlySet<string> SupportedServerCommands { get; } =
        new HashSet<string>(["Testnum", "response", "method", "user", "password", "backendport"], StringComparer.Ordinal);

    /// <summary>The reply code every request fails with, or <see langword="null"/> when requests connect.</summary>
    public int? Response { get; private set; }

    /// <summary>Whether the listener requires an RFC 1929 login (<c>method 2</c>).</summary>
    public bool RequiresLogin { get; private set; }

    /// <summary>The one user name accepted, or <see langword="null"/> for any.</summary>
    public string? User { get; private set; }

    /// <summary>The one password accepted with <see cref="User"/>, or <see langword="null"/> for any.</summary>
    public string? Password { get; private set; }

    /// <summary>The port every request connects to instead of the one it names, or <see langword="null"/>.</summary>
    public int? BackendPort { get; private set; }

    /// <summary>
    /// The proxy accounts the case's login requirement means: <see langword="null"/> when it has
    /// none.
    /// </summary>
    internal IAuthenticationPolicy? Accounts => RequiresLogin ? new SocksUpstreamTestCaseAccounts(User, Password) : null;

    private static int NumberOf(string command, string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : throw Unsupported(command);

    private static NotSupportedException Unsupported(string command) =>
        new($"The SOCKS server half does not implement the server command '{command}' with this value (servercmd-unsupported:{command}).");
}
