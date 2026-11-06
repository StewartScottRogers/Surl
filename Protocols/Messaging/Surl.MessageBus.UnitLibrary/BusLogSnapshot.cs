namespace Surl.MessageBus;

/// <summary>
/// One topic log in a <see cref="MessageBusSnapshot"/>: its topic, its log start offset, and the
/// messages it held from there on, in offset order.
/// </summary>
internal sealed record BusLogSnapshot(string Topic, long StartOffset, IReadOnlyList<BusMessage> Messages);
