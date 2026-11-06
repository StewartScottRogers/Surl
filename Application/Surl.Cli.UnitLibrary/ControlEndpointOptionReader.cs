using System.Net;
using System.Net.Sockets;
using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// Reads <c>--control</c> and checks it with <c>--control-token-file</c> by ADR-0124 decision 7:
/// the control endpoint listens on <c>http</c> only, on loopback only (<c>localhost</c>, an IPv4
/// address in <c>127.0.0.0/8</c> or <c>::1</c>), and each option needs the other. Every refusal is
/// <see cref="SurlExitCode.FailedInit"/>, before anything starts.
/// </summary>
internal static class ControlEndpointOptionReader
{
    /// <summary>The only scheme the control endpoint listens on.</summary>
    public const string ControlScheme = "http";

    /// <summary><c>--control</c> without <c>--control-token-file</c>: no token is ever generated.</summary>
    public const string NeedsTokenFile = "(2) --control needs --control-token-file";

    /// <summary><c>--control-token-file</c> without <c>--control</c>.</summary>
    public const string TokenFileWithoutControl = "(2) --control-token-file without --control";

    private const string SchemeSeparator = "://";
    private const string LocalhostName = "localhost";
    private const byte LoopbackFirstIpv4Byte = 127;
    private const int Ipv4PartCount = 4;

    /// <summary>The refusal of a control listen URL whose scheme is not <c>http</c>.</summary>
    /// <param name="scheme">The scheme as read, lower-cased.</param>
    public static string NeedsHttp(string scheme) => $"(2) --control needs {ControlScheme}, not {scheme}";

    /// <summary>The refusal of a control listen URL whose host is not loopback.</summary>
    /// <param name="argument">The listen URL as given.</param>
    public static string MustListenOnLoopback(string argument) =>
        $"(2) --control must listen on loopback (127.0.0.0/8, ::1 or localhost): {argument}";

    /// <summary>
    /// Reads <paramref name="argument"/> as the control listen URL: a scheme other than
    /// <c>http</c> is refused first, then a malformed listen URL as a bare argument's is, then a
    /// host that is not loopback.
    /// </summary>
    /// <param name="argument">The <c>--control</c> argument, never empty.</param>
    /// <returns>The listen URL, or the refusal.</returns>
    public static ListenUrlParseResult Read(string argument)
    {
        var separator = argument.IndexOf(SchemeSeparator, StringComparison.Ordinal);
        if (separator >= 0)
        {
            var scheme = argument[..separator].ToLowerInvariant();
            if (scheme != ControlScheme)
            {
                return ListenUrlParseResult.Refused(SurlExitCode.FailedInit, NeedsHttp(scheme));
            }
        }

        var parsed = ListenUrlParser.Parse(argument);
        if (!parsed.Succeeded)
        {
            return parsed;
        }

        return IsLoopbackHost(parsed.ListenUrl.Host)
            ? parsed
            : ListenUrlParseResult.Refused(SurlExitCode.FailedInit, MustListenOnLoopback(argument));
    }

    /// <summary>
    /// Refuses <c>--control</c> without <c>--control-token-file</c>, then the token file without
    /// <c>--control</c>; null when both or neither are given.
    /// </summary>
    /// <param name="commandLine">The whole command line, once read.</param>
    /// <returns>The refusal, or null.</returns>
    public static CommandLineFailure? RefuseCombination(SurlCommandLine commandLine)
    {
        if (commandLine.ControlListenUrl is not null && commandLine.ControlTokenFile is null)
        {
            return new CommandLineFailure(SurlExitCode.FailedInit, NeedsTokenFile);
        }

        return commandLine.ControlListenUrl is null && commandLine.ControlTokenFile is not null
            ? new CommandLineFailure(SurlExitCode.FailedInit, TokenFileWithoutControl)
            : null;
    }

    /// <summary>
    /// <see langword="true"/> for <c>localhost</c> in any case, <c>::1</c> with no zone, and a
    /// four-part IPv4 address whose first byte is 127. A host name is never resolved, and a
    /// shortened IPv4 form such as <c>127.1</c>, which the listen URL keeps as a host name, is not
    /// loopback.
    /// </summary>
    private static bool IsLoopbackHost(string host)
    {
        if (host.Equals(LocalhostName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6
            ? !host.Contains('%', StringComparison.Ordinal) && address.Equals(IPAddress.IPv6Loopback)
            : host.Split('.').Length == Ipv4PartCount && address.GetAddressBytes()[0] == LoopbackFirstIpv4Byte;
    }
}
