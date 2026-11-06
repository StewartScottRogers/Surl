namespace Surl.Control;

/// <summary>Sequence numbers the control endpoint's subscription skipped: events it never saw.</summary>
/// <param name="First">The first skipped sequence number.</param>
/// <param name="Last">The last skipped sequence number.</param>
internal sealed record ControlSequenceGap(long First, long Last);
