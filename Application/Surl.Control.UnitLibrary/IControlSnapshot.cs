namespace Surl.Control;

/// <summary>
/// A snapshot an <see cref="IControlTarget"/> took of its stores, opaque to the control endpoint: the endpoint only
/// keeps it under an ID and hands it back to <see cref="IControlTarget.RestoreAsync"/>.
/// </summary>
#pragma warning disable CA1040 // The snapshot's contents are the target's; the endpoint needs only its identity.
public interface IControlSnapshot;
#pragma warning restore CA1040
