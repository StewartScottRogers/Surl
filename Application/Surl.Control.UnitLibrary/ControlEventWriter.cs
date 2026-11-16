using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// Turns an event into its JSON (ADR-0124 decision 7, ADR-0171): <c>sequence</c>, <c>at</c> and <c>kind</c>, then
/// the members the event writes itself through a <see cref="ControlEventMemberWriter"/>, in snake_case, enum
/// values in snake_case, and an exchange's bytes as text or base64, at most <see cref="MaxShownBytesPerEvent"/>
/// of them.
/// </summary>
internal static class ControlEventWriter
{
    /// <summary>The most bytes of one event shown.</summary>
    public const int MaxShownBytesPerEvent = 65536;

    /// <summary>The JSON of <paramref name="surlEvent"/>.</summary>
    /// <param name="surlEvent">An event.</param>
    /// <returns>Its JSON shape.</returns>
    public static ControlEventJson ToJson(SurlEvent surlEvent)
    {
        var writer = new ControlEventMemberWriter(new ControlEventJson
        {
            Sequence = surlEvent.Sequence,
            At = Timestamp(surlEvent.At),
            Kind = surlEvent.KindName,
        });
        surlEvent.WriteMembers(writer);

        return writer.Written;
    }

    /// <summary>A time as the endpoint writes it: UTC, round-trip format.</summary>
    /// <param name="at">The time.</param>
    /// <returns>Such as <c>2026-11-05T10:00:00.0000000+00:00</c>.</returns>
    public static string Timestamp(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
