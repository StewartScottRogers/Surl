namespace Surl.Composition;

/// <summary>What a read-back of a store gave: its outcome, the value when found, and a sentence otherwise.</summary>
/// <typeparam name="T">The value read.</typeparam>
/// <param name="Outcome">How the read ended.</param>
/// <param name="Value">The value when <paramref name="Outcome"/> is <see cref="ComposedReadOutcome.Found"/>; otherwise <see langword="default"/>.</param>
/// <param name="Reason">Why it was not found or was refused, as a sentence; <see langword="null"/> when found.</param>
public sealed record ComposedReadResult<T>(ComposedReadOutcome Outcome, T? Value, string? Reason)
{
    /// <summary>Whether <see cref="Outcome"/> is <see cref="ComposedReadOutcome.Found"/>.</summary>
    public bool IsFound => Outcome == ComposedReadOutcome.Found;

    internal static ComposedReadResult<T> Of(T value) => new(ComposedReadOutcome.Found, value, null);

    internal static ComposedReadResult<T> Failed(ComposedReadOutcome outcome, string reason) => new(outcome, default, reason);
}
