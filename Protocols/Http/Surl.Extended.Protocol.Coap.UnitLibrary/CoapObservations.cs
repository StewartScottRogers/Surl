namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// Every CoAP observer of the process (RFC 7641; ADR-0097 decisions 0, 5 and 7): one object the
/// composition creates once and gives to every CoAP server type, so a change through one flow, or
/// one scheme, reaches the observers of every other.
/// </summary>
/// <remarks>
/// It holds at most <see cref="DefaultCapacity"/> observations; a registration past that is served
/// as a plain <c>GET</c>, without Observe (RFC 7641 section 4.1).
/// </remarks>
public sealed class CoapObservations
{
    /// <summary>The most observations the process keeps at once: 1000 (ADR-0097 decision 7).</summary>
    public const int DefaultCapacity = 1000;

    private readonly Lock gate = new();
    private readonly HashSet<CoapObservation> observations = [];
    private readonly int capacity;

    /// <summary>Creates an empty list of observers that holds at most <see cref="DefaultCapacity"/>.</summary>
    public CoapObservations()
        : this(DefaultCapacity)
    {
    }

    internal CoapObservations(int capacity) => this.capacity = capacity;

    /// <summary>How many observations are registered now.</summary>
    public int Count
    {
        get
        {
            lock (gate)
            {
                return observations.Count;
            }
        }
    }

    /// <summary>Adds <paramref name="observation"/> unless the list is full.</summary>
    /// <param name="observation">The observation.</param>
    /// <returns>Whether it was added.</returns>
    internal bool TryAdd(CoapObservation observation)
    {
        lock (gate)
        {
            return observations.Count < capacity && observations.Add(observation);
        }
    }

    /// <summary>Removes <paramref name="observation"/>, when it is there.</summary>
    /// <param name="observation">The observation.</param>
    internal void Remove(CoapObservation observation)
    {
        lock (gate)
        {
            observations.Remove(observation);
        }
    }

    /// <summary>
    /// Tells every observer of the resource at <paramref name="location"/> that it changed, so each
    /// is notified at once (RFC 7641 section 4.2).
    /// </summary>
    /// <param name="location">The content-store location of the resource that changed.</param>
    internal void MarkChanged(string location)
    {
        CoapObservation[] observing;

        lock (gate)
        {
            observing = [.. observations.Where(observation => string.Equals(observation.Location, location, StringComparison.Ordinal))];
        }

        foreach (var observation in observing)
        {
            observation.MarkChanged();
        }
    }
}
