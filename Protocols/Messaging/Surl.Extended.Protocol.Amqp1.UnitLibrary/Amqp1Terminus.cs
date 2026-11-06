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

    private const int DynamicFieldIndex = 4;

    private static readonly Amqp1Terminus None = new(null, false, false);

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

    private static Amqp1Terminus ReadSourceOrTarget(IReadOnlyList<object?> fields) =>
        new(fields.ElementAtOrDefault(AddressFieldIndex) as string, fields.ElementAtOrDefault(DynamicFieldIndex) is true, false);

    /// <summary>
    /// The container's source as a sender: the address, <c>dynamic</c> when asked, and the
    /// distribution mode, <c>copy</c> for a topic and <c>move</c> for a queue (ADR-0107 decision 4), and the
    /// <c>filter</c> entries it applies (ADR-0146 decision 2.1).
    /// </summary>
    public static Amqp1Described Source(string address, bool isDynamic, bool isTopic, Amqp1Map? appliedFilters) => new(
        Amqp1Descriptor.Source,
        Amqp1Fields.WithoutTrailingNulls([address, null, null, null, DynamicField(isDynamic), null, isTopic ? DistributionModeCopy : DistributionModeMove, appliedFilters]));

    /// <summary>The container's target as a receiver: the address, and <c>dynamic</c> when asked.</summary>
    public static Amqp1Described Target(string address, bool isDynamic) => new(
        Amqp1Descriptor.Target,
        Amqp1Fields.WithoutTrailingNulls([address, null, null, null, DynamicField(isDynamic)]));

    private static object? DynamicField(bool isDynamic) => isDynamic ? true : null;
}
