namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// What the protocol-header and SASL negotiation of ADR-0107 decisions 1 and 2 may allow.
/// </summary>
public sealed record Amqp1SecurityNegotiationOptions
{
    /// <summary>The options with every allowance off: SASL required, no TLS header answered.</summary>
    public static Amqp1SecurityNegotiationOptions Default { get; } = new();

    /// <summary>
    /// <c>--allow-anonymous</c>: <see langword="true"/> lets the plain AMQP header through without
    /// SASL, and offers and accepts the <c>ANONYMOUS</c> mechanism.
    /// </summary>
    public bool AllowsAnonymous { get; init; }

    /// <summary>
    /// <see langword="true"/> when the listener has a certificate (<c>--cert</c> or
    /// <c>--self-signed</c>), so a TLS header on a plaintext connection is answered and upgraded.
    /// </summary>
    public bool CanUpgradeToTls { get; init; }
}
