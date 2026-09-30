namespace Surl.Protocol.Smtp;

/// <summary>
/// Holds a message body as <c>CrlfLineReader.ReadDotStuffedBodyAsync</c> writes it, up to a
/// budget: <c>--max-filesize</c> less the trace fields' length (ADR-0053, decision 6). Past the
/// budget it counts what it is given and keeps nothing more.
/// </summary>
/// <param name="budgetBytes">The most body bytes kept; may be negative when the trace fields
/// alone are past the limit.</param>
internal sealed class SmtpMessageBodyBuffer(long budgetBytes) : MemoryStream
{
    /// <summary>
    /// How many body bytes were written, kept or not.
    /// </summary>
    public long ReceivedBytes { get; private set; }

    /// <summary>
    /// Whether the body is past the budget.
    /// </summary>
    public bool IsPastBudget => ReceivedBytes > budgetBytes;

    /// <summary>
    /// Keeps <paramref name="buffer"/> while the body is within the budget; the one write the
    /// body reader makes.
    /// </summary>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ReceivedBytes += buffer.Length;
        return IsPastBudget ? ValueTask.CompletedTask : base.WriteAsync(buffer, cancellationToken);
    }
}
