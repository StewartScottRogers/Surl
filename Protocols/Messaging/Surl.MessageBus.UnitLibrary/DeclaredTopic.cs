namespace Surl.MessageBus;

/// <summary>
/// One <c>--topic &lt;filter&gt;[;retain][;qos=&lt;0|1|2&gt;]</c> (ADR-0102, decision 3.1): a
/// canonical filter whose attributes apply to every publish on a topic it matches. Several
/// matching declarations combine: retained if any says so, the lowest QoS ceiling. A filter
/// without wildcards is a topic whose log exists from start.
/// </summary>
public sealed record DeclaredTopic
{
    /// <summary>
    /// Declares a topic filter.
    /// </summary>
    /// <param name="filter">A canonical filter (<see cref="CanonicalTopicName.IsValidFilter"/>).</param>
    /// <param name="retain">Whether every publish on a matching topic is retained, as if its
    /// retain flag were set.</param>
    /// <param name="maximumQualityOfService">The highest QoS the bus carries a matching publish
    /// at; <see langword="null"/> for no ceiling.</param>
    /// <exception cref="ArgumentException"><paramref name="filter"/> is not a canonical filter.</exception>
    public DeclaredTopic(string filter, bool retain = false, BusQualityOfService? maximumQualityOfService = null)
    {
        if (!CanonicalTopicName.IsValidFilter(filter))
        {
            throw new ArgumentException($"'{filter}' is not a canonical topic filter.", nameof(filter));
        }

        Filter = filter;
        Retain = retain;
        MaximumQualityOfService = maximumQualityOfService;
    }

    /// <summary>The canonical filter.</summary>
    public string Filter { get; }

    /// <summary>Whether every publish on a matching topic is retained.</summary>
    public bool Retain { get; }

    /// <summary>The highest QoS a matching publish is carried at; <see langword="null"/> for none.</summary>
    public BusQualityOfService? MaximumQualityOfService { get; }
}
