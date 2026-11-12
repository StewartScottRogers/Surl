namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// What the container reads of a link's source or target (Part 3 sections 3.5.3 and 3.5.4) and a
/// transaction <c>coordinator</c> (Part 4 section 4.5.1), and the terminus it answers with.
/// </summary>
/// <param name="Address">The <c>address</c> field when it is a string; otherwise <see langword="null"/>.</param>
/// <param name="IsDynamic">Whether the <c>dynamic</c> field is true.</param>
/// <param name="IsCoordinator">Whether the terminus is a transaction coordinator.</param>
internal sealed record Amqp1Terminus(string? Address, bool IsDynamic, bool IsCoordinator)
{
    private const int AddressFieldIndex = 0;

    private const int DurableFieldIndex = 1;

    private const int ExpiryPolicyFieldIndex = 2;

    private const int TimeoutFieldIndex = 3;

    private const int DynamicFieldIndex = 4;

    private const int CapabilitiesFieldIndex = 10;

    private static readonly Amqp1Terminus None = new(null, false, false);

    private static readonly Amqp1Symbol SharedCapability = new("shared");

    private static readonly Amqp1Symbol GlobalCapability = new("global");

    private static readonly Amqp1Symbol DistributionModeMove = new("move");

    private static readonly Amqp1Symbol DistributionModeCopy = new("copy");

    /// <summary>Reads a terminus field of an <c>attach</c>; anything but a source, target or coordinator reads as no address.</summary>
    public static Amqp1Terminus Read(object? value) =>
        value is Amqp1Described { Value: IReadOnlyList<object?> fields } described
            ? ReadDescribedList(Amqp1Descriptor.CodeOf(described.Descriptor), fields)
            : None;

    private static Amqp1Terminus ReadDescribedList(ulong? descriptorCode, IReadOnlyList<object?> fields) => descriptorCode switch
    {
        Amqp1Descriptor.Coordinator => new(null, false, true),
        Amqp1Descriptor.Source or Amqp1Descriptor.Target => ReadSourceOrTarget(fields),
        _ => None,
    };

    private static Amqp1Terminus ReadSourceOrTarget(IReadOnlyList<object?> fields)
    {
        var capabilities = CapabilitySymbols(fields.ElementAtOrDefault(CapabilitiesFieldIndex));
        return new(fields.ElementAtOrDefault(AddressFieldIndex) as string, fields.ElementAtOrDefault(DynamicFieldIndex) is true, false)
        {
            Durable = fields.ElementAtOrDefault(DurableFieldIndex) is uint durable ? durable : 0,
            ExpiryPolicy = fields.ElementAtOrDefault(ExpiryPolicyFieldIndex) is Amqp1Symbol policy ? policy.Value : null,
            TimeoutSeconds = fields.ElementAtOrDefault(TimeoutFieldIndex) is uint timeout ? timeout : 0,
            IsShared = capabilities.Contains(SharedCapability),
            IsGlobal = capabilities.Contains(GlobalCapability),
        };
    }

    private static IReadOnlyList<object?> CapabilitySymbols(object? field) => field switch
    {
        Amqp1Symbol symbol => [symbol],
        Amqp1Array array => array.Elements,
        _ => [],
    };

    /// <summary>The <c>durable</c> field (Part 3 section 3.5.5): 0 none, 1 configuration, 2 unsettled-state; 0 when absent.</summary>
    public uint Durable { get; init; }

    /// <summary>The <c>expiry-policy</c> field's symbol (Part 3 section 3.5.6); <see langword="null"/> when absent.</summary>
    public string? ExpiryPolicy { get; init; }

    /// <summary>The <c>timeout</c> field in seconds; 0 when absent.</summary>
    public uint TimeoutSeconds { get; init; }

    /// <summary>Whether the <c>capabilities</c> include <c>shared</c> (ADR-0146 decision 3.5).</summary>
    public bool IsShared { get; init; }

    /// <summary>Whether the <c>capabilities</c> include <c>global</c> (ADR-0146 decision 3.5).</summary>
    public bool IsGlobal { get; init; }

    /// <summary>
    /// The container's source as a sender: the address, <c>dynamic</c> when asked, and the
    /// distribution mode, <c>copy</c> for a topic and <c>move</c> for a queue (ADR-0107 decision 4), and the
    /// <c>filter</c> entries it applies (ADR-0146 decision 2.1).
    /// </summary>
    public static Amqp1Described Source(string address, bool isDynamic, bool isTopic, Amqp1Map? appliedFilters) =>
        Source(address, isDynamic, isTopic, appliedFilters, null);

    /// <summary>
    /// The container's source as <see cref="Source(string, bool, bool, Amqp1Map?)"/> writes it, with the
    /// <c>durable</c>, <c>expiry-policy</c>, <c>timeout</c> and <c>capabilities</c> of a kept subscription
    /// when <paramref name="kept"/> is given (ADR-0146 decision 3).
    /// </summary>
    public static Amqp1Described Source(string address, bool isDynamic, bool isTopic, Amqp1Map? appliedFilters, Amqp1KeptSubscription? kept) => new(
        Amqp1Descriptor.Source,
        Amqp1Fields.WithoutTrailingNulls(
        [
            address,
            kept is null ? null : 1u,
            kept?.ExpiryPolicy,
            kept is { TimeoutSeconds: > 0 } ? kept.TimeoutSeconds : null,
            DynamicField(isDynamic),
            null,
            isTopic ? DistributionModeCopy : DistributionModeMove,
            appliedFilters,
            null,
            null,
            kept is null ? null : Amqp1Fields.SymbolsField(kept.Capabilities),
        ]));

    /// <summary>The container's target as a receiver: the address, and <c>dynamic</c> when asked.</summary>
    public static Amqp1Described Target(string address, bool isDynamic) => new(
        Amqp1Descriptor.Target,
        Amqp1Fields.WithoutTrailingNulls([address, null, null, null, DynamicField(isDynamic)]));

    /// <summary>
    /// The container's <c>coordinator</c> (Part 4 sections 4.5.1 and 4.5.7): its <c>capabilities</c> are the
    /// ones the container serves, local transactions, several a session and several sessions a
    /// transaction (ADR-0146 decision 1.1).
    /// </summary>
    public static Amqp1Described Coordinator { get; } = new(
        Amqp1Descriptor.Coordinator,
        new object?[] { Amqp1Fields.SymbolsField([new("amqp:local-transactions"), new("amqp:multi-txns-per-ssn"), new("amqp:multi-ssns-per-txn")]) });

    private static object? DynamicField(bool isDynamic) => isDynamic ? true : null;
}
