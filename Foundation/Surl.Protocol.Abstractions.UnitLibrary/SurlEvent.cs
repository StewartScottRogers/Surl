using System.Text;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// One thing that happened on a Surl instance, published on its event stream (ADR-0124, decision 1).
/// No event carries a password, key, token or other secret. An event says what hosts read from it
/// (<see cref="ExchangeId"/>, <see cref="Scheme"/>, <see cref="Account"/>, <see cref="KindName"/>,
/// <see cref="WriteMembers"/>), so no host switches on its type (ADR-0171).
/// </summary>
public abstract record SurlEvent
{
    /// <summary>
    /// The event's place on its stream: 1, 2, 3 ... with no gaps, stamped by the stream; 0 until stamped.
    /// </summary>
    public long Sequence { get; init; }

    /// <summary>
    /// When the stream received the event, stamped from the stream's <see cref="TimeProvider"/>;
    /// default until stamped.
    /// </summary>
    public DateTimeOffset At { get; init; }

    /// <summary>
    /// The exchange the event belongs to, or <see langword="null"/> when it belongs to none.
    /// A record that has the fact overrides this.
    /// </summary>
    public virtual long? ExchangeId => null;

    /// <summary>
    /// The scheme the event was served over, or <see langword="null"/> when it names none.
    /// A record that has the fact overrides this.
    /// </summary>
    public virtual string? Scheme => null;

    /// <summary>
    /// The account the event is about, or <see langword="null"/> when it names none.
    /// A record that has the fact overrides this.
    /// </summary>
    public virtual string? Account => null;

    /// <summary>
    /// The event's kind: its type name without the <c>Event</c> suffix, in snake_case
    /// (<c>ExchangeStartedEvent</c> is <c>exchange_started</c>).
    /// </summary>
    public string KindName
    {
        get
        {
            var typeName = GetType().Name;
            var kind = typeName.EndsWith("Event", StringComparison.Ordinal) ? typeName[..^"Event".Length] : typeName;
            var snake = new StringBuilder(kind.Length + 4);
            foreach (var letter in kind)
            {
                if (char.IsUpper(letter) && snake.Length > 0)
                {
                    snake.Append('_');
                }

                snake.Append(char.ToLowerInvariant(letter));
            }

            return snake.ToString();
        }
    }

    /// <summary>
    /// Writes the members this record declares, other than <see cref="Sequence"/> and <see cref="At"/>,
    /// to <paramref name="writer"/>. A record overrides this to write each of its public properties.
    /// </summary>
    /// <param name="writer">Where the members go.</param>
    public virtual void WriteMembers(ISurlEventMemberWriter writer)
    {
    }
}
