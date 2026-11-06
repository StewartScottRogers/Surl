namespace Surl.Extended.Protocol.Amqp1;

/// <summary>What an attach asks of <see cref="IAmqp1NodeDirectory"/>.</summary>
/// <param name="CanonicalAddress">
/// The node's address as <see cref="Amqp1NodeAddress.Canonical"/> gives it; <see langword="null"/> for a
/// dynamic node, which the directory names (Part 3 section 3.5.3).
/// </param>
/// <param name="ClientIsReceiver">Whether the client receives from the node (its source) rather than sends to it (its target).</param>
/// <param name="AccountName">The account the client logged in as; <see langword="null"/> when anonymous.</param>
public sealed record Amqp1NodeRequest(string? CanonicalAddress, bool ClientIsReceiver, string? AccountName)
{
    /// <summary>Whether the client asked for a dynamic node.</summary>
    public bool IsDynamic => CanonicalAddress is null;
}
