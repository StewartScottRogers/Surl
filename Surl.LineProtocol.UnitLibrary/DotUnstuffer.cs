namespace Surl.LineProtocol;

/// <summary>
/// Removes SMTP dot-stuffing from a body arriving in pieces and finds its end, CRLF <c>.</c>
/// CRLF (RFC 5321, section 4.5.2). The body's first line counts as following a CRLF.
/// </summary>
/// <remarks>
/// Only CRLF starts a line: a bare LF or a bare CR is body and never ends it, so
/// <c>LF . LF</c> is body. The first <c>.</c> of a line that starts with one is removed. The
/// body's final CRLF is kept; the terminator's <c>.</c> CRLF is not.
/// </remarks>
internal sealed class DotUnstuffer
{
    private State state = State.LineStart;

    private enum State
    {
        LineStart,
        InLine,
        AfterCr,
        DotAtLineStart,
        DotCrAtLineStart,
    }

    /// <summary>
    /// Unstuffs <paramref name="input"/> into <paramref name="output"/>, stopping after the terminator.
    /// </summary>
    /// <param name="input">The next bytes of the body.</param>
    /// <param name="output">Where the unstuffed bytes go; one byte longer than <paramref name="input"/> always suffices.</param>
    /// <returns>How many input bytes were consumed and output bytes written, and whether the terminator was reached.</returns>
    public DotUnstuffStep Unstuff(ReadOnlySpan<byte> input, Span<byte> output)
    {
        var written = 0;
        for (var index = 0; index < input.Length; index++)
        {
            if (Step(input[index], output, ref written))
            {
                return new DotUnstuffStep(index + 1, written, true);
            }
        }

        return new DotUnstuffStep(input.Length, written, false);
    }

    // Takes one byte; returns true when it completes the terminator.
    private bool Step(byte next, Span<byte> output, ref int written)
    {
        if (HoldsBack(next))
        {
            return false;
        }

        if (state == State.DotCrAtLineStart)
        {
            if (next == '\n')
            {
                return true;
            }

            Emit((byte)'\r', output, ref written);
        }

        Emit(next, output, ref written);
        return false;
    }

    // Holds back a dot starting a line, and a CR after it, until the next byte says whether
    // they are stuffing, the terminator or body; returns true when it held next back.
    private bool HoldsBack(byte next)
    {
        if (state == State.LineStart && next == '.')
        {
            state = State.DotAtLineStart;
            return true;
        }

        if (state == State.DotAtLineStart && next == '\r')
        {
            state = State.DotCrAtLineStart;
            return true;
        }

        return false;
    }

    private void Emit(byte next, Span<byte> output, ref int written)
    {
        output[written++] = next;
        state = next == '\r' ? State.AfterCr : NextStateAfter(next);
    }

    private State NextStateAfter(byte next) =>
        state == State.AfterCr && next == '\n' ? State.LineStart : State.InLine;
}
