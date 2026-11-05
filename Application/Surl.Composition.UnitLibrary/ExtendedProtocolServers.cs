using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The one place <c>surl</c> composes its extended protocol servers, the
/// <c>Surl.Extended.Protocol.*</c> servers for schemes the Curl port adds and upstream curl does
/// not have, and the one switch that turns them all off (ADR-0083, decisions 6 and 7).
/// </summary>
/// <remarks>
/// An extended server joins with one entry in <see cref="Registrations"/>; its schemes' default ports
/// are already rows of <c>Surl.Cli</c>'s <c>ExtendedSchemeDefaultPorts</c>. The original servers
/// are <see cref="ProtocolServerRegistrations.Original"/> and are never touched by this class.
/// </remarks>
public static class ExtendedProtocolServers
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
    /// Gets every extended protocol server's entry this build has, in ordinal order of its first
    /// scheme, on the same <see cref="ProtocolServerRegistration"/> as the originals (ADR-0124,
    /// decision 2); none yet.
    /// </summary>
    public static IReadOnlyList<ProtocolServerRegistration> Registrations { get; } = [];

    /// <summary>
    /// Builds every extended protocol server <see cref="Registrations"/> lists, in its order.
    /// </summary>
    /// <param name="services">What the servers are built from.</param>
    /// <returns>The servers.</returns>
    public static IReadOnlyList<IProtocolServer> Compose(ExtendedProtocolServerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return SurlServerComposition.ComposeProtocolServers(Registrations, services.ComposedServices);
    }
}
