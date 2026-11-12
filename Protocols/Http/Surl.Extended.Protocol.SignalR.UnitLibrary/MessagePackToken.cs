namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// One MessagePack value's first byte or bytes, read.
/// </summary>
/// <param name="Kind">What kind of value it is.</param>
/// <param name="Number">
/// An integer's value (a 64-bit unsigned value past <see cref="long.MaxValue"/> reads as
/// <see cref="long.MaxValue"/>); a boolean's 0 or 1; an array's or map's entry count; or the number
/// of payload bytes that follow a string, byte array, extension or float.
/// </param>
internal readonly record struct MessagePackToken(MessagePackKind Kind, long Number);
