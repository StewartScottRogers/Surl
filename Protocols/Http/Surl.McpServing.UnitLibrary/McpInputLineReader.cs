using System.Buffers;

namespace Surl.McpServing;

/// <summary>
/// Reads newline-delimited messages from a stream for <see cref="McpStreamSession" /> (ADR-0103 decision 1):
/// a trailing <c>\r</c> before the <c>\n</c> is dropped, an empty line is skipped, and a line longer than
/// the message limit is read to its newline and discarded without being buffered.
/// </summary>
internal sealed class McpInputLineReader
{
    private const byte LineFeed = (byte)'\n';

    private const byte CarriageReturn = (byte)'\r';

    private readonly Stream _input;

    private readonly int _maximumMessageBytes;

    private readonly byte[] _buffer;

    private readonly ArrayBufferWriter<byte> _line = new();

    private int _start;

    private int _end;

    private bool _discarding;

    /// <summary>Initializes a new instance of the <see cref="McpInputLineReader" /> class.</summary>
    /// <param name="input">The stream the client writes its messages to.</param>
    /// <param name="maximumMessageBytes">The longest message accepted, in bytes, the line terminator excluded.</param>
    /// <param name="bufferBytes">How many bytes one read from <paramref name="input" /> asks for.</param>
    internal McpInputLineReader(Stream input, int maximumMessageBytes, int bufferBytes = 16 * 1024)
    {
        _input = input;
        _maximumMessageBytes = maximumMessageBytes;
        _buffer = new byte[bufferBytes];
    }

    /// <summary>Reads the next message.</summary>
    /// <param name="cancellationToken">Cancels the wait for input.</param>
    /// <returns>
    /// The next message's bytes; an empty array for a line that was longer than the limit and was discarded;
    /// or <see langword="null" /> when the input has ended. An unterminated last line counts as a message.
    /// </returns>
    internal async Task<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (_start == _end)
            {
                _start = 0;
                _end = await _input.ReadAsync(_buffer, cancellationToken).ConfigureAwait(false);
                if (_end == 0)
                {
                    return EndLine();
                }
            }

            if (TakeBufferedLine() is { } line)
            {
                return line;
            }
        }
    }

    // Consumes buffered bytes up to and including the next newline; returns a finished line, or null when
    // the buffer ran out first or the line was empty.
    private byte[]? TakeBufferedLine()
    {
        ReadOnlySpan<byte> buffered = _buffer.AsSpan(_start, _end - _start);
        int newline = buffered.IndexOf(LineFeed);
        ReadOnlySpan<byte> taken = newline < 0 ? buffered : buffered[..newline];
        Append(taken);
        _start += newline < 0 ? taken.Length : newline + 1;
        return newline < 0 ? null : EndLine();
    }

    // The limit excludes the terminator, so one byte more is kept for a \r that may precede the newline.
    private void Append(ReadOnlySpan<byte> bytes)
    {
        if (_discarding)
        {
            return;
        }

        if (_line.WrittenCount + bytes.Length > _maximumMessageBytes + 1)
        {
            _discarding = true;
            _line.Clear();
            return;
        }

        _line.Write(bytes);
    }

    // Ends the line being read: null for an empty line (or none at the end of input), an empty array for a
    // discarded one, and the message's bytes otherwise.
    private byte[]? EndLine()
    {
        if (_discarding)
        {
            _discarding = false;
            return [];
        }

        ReadOnlySpan<byte> line = _line.WrittenSpan;
        if (line.EndsWith(CarriageReturn))
        {
            line = line[..^1];
        }

        byte[]? message = line.Length switch
        {
            0 => null,
            _ when line.Length > _maximumMessageBytes => [],
            _ => line.ToArray(),
        };
        _line.Clear();
        return message;
    }
}
