namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// What the protocol-header and SASL negotiation of ADR-0107 decisions 1 and 2 may allow.
/// </summary>
public sealed record Amqp1SecurityNegotiationOptions
{
    /// <summary>The options with every allowance off: SASL required, no TLS upgrade.</summary>
    public static Amqp1SecurityNegotiationOptions Default { get; } = new();

    /// <summary>
    /// <c>--allow-anonymous</c>: <see langword="true"/> lets the plain AMQP header through without
    /// SASL, and offers and accepts the <c>ANONYMOUS</c> mechanism.
    /// </summary>
    public bool AllowsAnonymous { get; init; }

    /// <summary>
    /// <see langword="true"/> when a TLS upgrade is available, so a TLS header on a plaintext
    /// connection is answered and upgraded: a certificate is configured (<c>--cert</c> or
    /// <c>--self-signed</c>) or TLS-SRP is served (ADR-0122 decision 5). <c>Surl.Composition</c>
    /// passes <c>ServerTlsComposition.IsTlsUpgradeAvailable</c>.
    /// </summary>
    public bool CanUpgradeToTls { get; init; }
}
