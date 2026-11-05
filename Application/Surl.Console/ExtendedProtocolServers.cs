using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// The one place <c>surl</c> composes its extended protocol servers, the
/// <c>Surl.Extended.Protocol.*</c> servers for schemes the Curl port adds and upstream curl does
/// not have, and the one switch that turns them all off (ADR-0083, decisions 6 and 7).
/// </summary>
/// <remarks>
/// An extended server joins with one line in <see cref="Compose"/>; its schemes' default ports
/// are already rows of <c>Surl.Cli</c>'s <c>ExtendedSchemeDefaultPorts</c>. The original servers
/// are composed by <see cref="CommandLineRunner"/> and are never touched by this class.
/// </remarks>
internal static class ExtendedProtocolServers
{
    /// <summary>The environment variable whose value <see cref="TurnedOffValue"/> turns every extended server off.</summary>
    public const string SwitchVariable = "SURL_EXTENSIONS";

    /// <summary>The value of <see cref="SwitchVariable"/> that turns every extended server off.</summary>
    public const string TurnedOffValue = "0";

    /// <summary>
    /// Tells whether the extended servers are on: <see cref="SwitchVariable"/> unset, empty or
    /// any value but <see cref="TurnedOffValue"/> leaves them on, mirroring the Curl port's
    /// <c>CURL_EXTENSIONS=0</c>.
    /// </summary>
    /// <param name="environmentVariables">Reads the process's environment.</param>
    /// <returns><see langword="true"/> unless <c>SURL_EXTENSIONS=0</c>.</returns>
    public static bool AreTurnedOn(IEnvironmentVariableReader environmentVariables) =>
        environmentVariables.Read(SwitchVariable) != TurnedOffValue;

    /// <summary>
    /// Builds every extended protocol server this build has, in ordinal order of its first scheme.
    /// </summary>
    /// <param name="services">What the servers are built from.</param>
    /// <returns>The servers; none yet.</returns>
    public static IReadOnlyList<IProtocolServer> Compose(ExtendedProtocolServerServices services) => [];
}
