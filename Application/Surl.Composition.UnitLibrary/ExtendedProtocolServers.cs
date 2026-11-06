using Surl.Extended.Protocol.S3;
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
    /// decision 2): today the S3 server for <c>s3</c> (BL-421).
    /// </summary>
    /// <remarks>
    /// The S3 server answers <c>s3</c> over the one content store, its object metadata index and the
    /// policy over the accounts allowed on <c>s3</c> (ADR-0108; ADR-0102 decision 4.3); <c>s3</c> is TLS from the first byte (ADR-0108, decision 5), which
    /// the serving engine reads from <see cref="TlsSchemes"/>, so it needs no second registration.
    /// </remarks>
    public static IReadOnlyList<ProtocolServerRegistration> Registrations { get; } =
    [
        new(["s3"], services => new S3ProtocolServer(
            services.ContentStore, services.ObjectMetadataIndex, services.AuthenticationPolicyFor("s3"), services.TimeProvider)),
    ];

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
