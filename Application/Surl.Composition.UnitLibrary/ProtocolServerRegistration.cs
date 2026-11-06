using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// One entry of an ordered protocol server registration list (ADR-0124, decision 2): the schemes the
/// server answers and how it is built from the composed services. A new protocol server joins by
/// adding one entry to <see cref="ProtocolServerRegistrations.Original"/> or
/// <see cref="ExtendedProtocolServers.Registrations"/>; no reflection, no assembly scanning.
/// </summary>
/// <param name="Schemes">The schemes the server built by <paramref name="Create"/> answers, in its own order.</param>
/// <param name="Create">Builds the server from the services composed for this start.</param>
public sealed record ProtocolServerRegistration(
    IReadOnlyList<string> Schemes,
    Func<ComposedProtocolServices, IProtocolServer> Create)
{
    /// <summary>
    /// Registers a scheme that is TLS from the first byte and answered by the server an earlier
    /// entry of the same list composed for <paramref name="plainScheme"/>, through
    /// <see cref="ImplicitTlsSchemeServer"/> over a connection the engine has secured (ADR-0020).
    /// </summary>
    /// <param name="scheme">The implicit-TLS scheme, such as <c>https</c>.</param>
    /// <param name="plainScheme">The scheme of the server that answers it, such as <c>http</c>.</param>
    /// <returns>The entry.</returns>
    public static ProtocolServerRegistration ImplicitTls(string scheme, string plainScheme) =>
        new([scheme], services => new ImplicitTlsSchemeServer((IConnectionProtocolServer)services.ComposedServerFor(plainScheme), scheme));
}
