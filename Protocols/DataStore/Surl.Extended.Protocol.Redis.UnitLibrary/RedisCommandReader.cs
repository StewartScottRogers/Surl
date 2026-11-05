using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// Reads one connection's commands, one at a time and in order, however their bytes are split
/// across reads (the RESP page, "Sending commands to a Redis server", inline commands and
/// pipelining; ADR-0106 decisions 2 and 6).
/// </summary>
/// <remarks>
/// <para>
/// A command starting with <c>*</c> is an array of bulk strings; any other line is an inline
/// command (<see cref="RedisInlineCommandSplitter"/>). A blank inline line, <c>*0</c> and a
/// negative multibulk count are skipped, as Redis does.
/// </para>
/// <para>
/// Framing errors are <see cref="RedisCommandReadOutcomeKind.ProtocolError"/> with the fixed
/// text of ADR-0106 decision 6: a bulk length that is negative, not a canonical decimal, above
/// <c>--max-filesize</c> or not followed by CR LF after its bytes is <c>invalid bulk length</c>; a
/// multibulk count that is not a canonical decimal or above 1048576 is <c>invalid multibulk
/// length</c>; an element not starting with <c>$</c> is <c>expected '$', got '&lt;byte&gt;'</c>; an
/// inline line longer than <c>--max-line</c> is <c>too big inline request</c>, and one with
/// unbalanced quotes <c>unbalanced quotes in request</c>. A <c>*</c> or <c>$</c> line longer than
/// <c>--max-line</c> is the invalid length of its kind.
/// </para>
/// </remarks>
public sealed class RedisCommandReader
{
    /// <summary>The most elements one command may have (Redis's bound before 7.0).</summary>
    public const int MaxMultibulkCount = 1048576;

    /// <summary>The bytes one command may hold beyond its largest bulk string (Redis's query buffer limit).</summary>
    public const long CommandFramingAllowance = 65536;

    /// <summary>The most elements one command may have before a login (Redis 7's bound).</summary>
    public const int UnauthenticatedMaxMultibulkCount = 10;

    /// <summary>The longest bulk string one command may hold before a login (Redis 7's bound).</summary>
    public const int UnauthenticatedMaxBulkBytes = 16384;

    private const int InitialBufferSize = 16384;

    private readonly IConnection connection;
    private readonly long maxBulkBytes;
    private readonly long maxLineBytes;
    private readonly long maxCommandBytes;
    private byte[] buffer = new byte[InitialBufferSize];
    private int start;
    private int end;
    private long commandBytes;

    /// <summary>Creates a reader of <paramref name="connection"/>'s commands.</summary>
    /// <param name="connection">The connection read from.</param>
    /// <param name="limits">
    /// The exchange's limits: <see cref="ExchangeLimits.MaxUploadBytes"/> (<c>--max-filesize</c>)
    /// bounds a bulk string and <see cref="ExchangeLimits.MaxLineBytes"/> (<c>--max-line</c>, CR LF
    /// included) a line.
    /// </param>
    public RedisCommandReader(IConnection connection, ExchangeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(limits);
        this.connection = connection;
        maxBulkBytes = Math.Min(limits.MaxUploadBytes, Array.MaxLength - 2);
        maxLineBytes = limits.MaxLineBytes;
        maxCommandBytes = limits.MaxUploadBytes + CommandFramingAllowance;
    }

    /// <summary>
    /// Whether the next commands are read under the limits Redis 7 puts on a client that has not
    /// logged in (ADR-0106 decision 6): more than <see cref="UnauthenticatedMaxMultibulkCount"/>
    /// elements is <c>unauthenticated multibulk length</c>, a bulk string over
    /// <see cref="UnauthenticatedMaxBulkBytes"/> <c>unauthenticated bulk length</c>. The server sets
    /// it before each read, by whether the connection may yet run every command.
    /// </summary>
    public bool AppliesUnauthenticatedLimits { get; set; }

    /// <summary>
    /// Waits until the next command's first byte has arrived, so a server can time the command
    /// from its first byte rather than from the end of the one before (ADR-0106 decision 6).
    /// </summary>
    /// <param name="cancellationToken">Ends the wait.</param>
    /// <returns><see langword="false"/> when the client closed its side first.</returns>
    public async ValueTask<bool> WaitForCommandStartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RequireBytesAsync(1, cancellationToken);
            return true;
        }
        catch (CommandNotReadException)
        {
            return false;
        }
    }

    /// <summary>Reads the next command, skipping empty ones.</summary>
    /// <param name="cancellationToken">Ends the read.</param>
    /// <returns>The command, or why there is none.</returns>
    public async ValueTask<RedisCommandReadOutcome> ReadCommandAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                commandBytes = 0;
                await RequireBytesAsync(1, cancellationToken);
                var command = buffer[start] == (byte)'*'
                    ? await ReadMultibulkCommandAsync(cancellationToken)
                    : await ReadInlineCommandAsync(cancellationToken);

                if (command is not null)
                {
                    return RedisCommandReadOutcome.Command(command);
                }
            }
        }
        catch (CommandNotReadException notRead)
        {
            return notRead.Outcome;
        }
    }

    private async ValueTask<List<byte[]>?> ReadMultibulkCommandAsync(CancellationToken cancellationToken)
    {
        var invalidLength = RedisCommandReadOutcome.ProtocolError("invalid multibulk length"u8);
        var header = await ReadLineAsync(invalidLength, cancellationToken);

        if (!TryParseHeaderNumber(header, out var count) || count > MaxMultibulkCount)
        {
            throw new CommandNotReadException(invalidLength);
        }

        ThrowIfPastUnauthenticatedLimit(count, UnauthenticatedMaxMultibulkCount, "unauthenticated multibulk length"u8);

        var arguments = new List<byte[]>();

        for (var index = 0L; index < count; index++)
        {
            arguments.Add(await ReadBulkStringAsync(cancellationToken));
        }

        return arguments.Count == 0 ? null : arguments;
    }

    private async ValueTask<byte[]> ReadBulkStringAsync(CancellationToken cancellationToken)
    {
        await RequireBytesAsync(1, cancellationToken);

        if (buffer[start] != (byte)'$')
        {
            var got = buffer[start] is (byte)'\r' or (byte)'\n' ? (byte)' ' : buffer[start];
            throw new CommandNotReadException(RedisCommandReadOutcome.ProtocolError([.. "expected '$', got '"u8, got, (byte)'\'']));
        }

        var invalidLength = RedisCommandReadOutcome.ProtocolError("invalid bulk length"u8);
        var header = await ReadLineAsync(invalidLength, cancellationToken);

        if (!TryParseHeaderNumber(header, out var length) || length < 0 || length > maxBulkBytes)
        {
            throw new CommandNotReadException(invalidLength);
        }

        ThrowIfPastUnauthenticatedLimit(length, UnauthenticatedMaxBulkBytes, "unauthenticated bulk length"u8);

        CountCommandBytes(length + 2);
        await RequireBytesAsync((int)length + 2, cancellationToken);
        var lineEnd = start + (int)length;

        if (buffer[lineEnd] != (byte)'\r' || buffer[lineEnd + 1] != (byte)'\n')
        {
            throw new CommandNotReadException(invalidLength);
        }

        var bytes = buffer[start..lineEnd];
        start = lineEnd + 2;
        return bytes;
    }

    private async ValueTask<List<byte[]>?> ReadInlineCommandAsync(CancellationToken cancellationToken)
    {
        var line = await ReadLineAsync(RedisCommandReadOutcome.ProtocolError("too big inline request"u8), cancellationToken);
        var text = line.Length > 0 && line[^1] == (byte)'\r' ? line.AsSpan(0, line.Length - 1) : line;
        var arguments = RedisInlineCommandSplitter.Split(text)
            ?? throw new CommandNotReadException(RedisCommandReadOutcome.ProtocolError("unbalanced quotes in request"u8));
        return arguments.Count == 0 ? null : arguments;
    }

    /// <summary>
    /// Reads up to the next LF and returns the line before it (a CR included); a line longer than
    /// <c>--max-line</c> with its LF is <paramref name="tooLong"/>.
    /// </summary>
    private async ValueTask<byte[]> ReadLineAsync(RedisCommandReadOutcome tooLong, CancellationToken cancellationToken)
    {
        var searched = 0;

        while (true)
        {
            var lineFeed = Array.IndexOf(buffer, (byte)'\n', start + searched, end - start - searched);
            var lineLength = (lineFeed < 0 ? end : lineFeed) - start + 1;

            if (lineLength > maxLineBytes)
            {
                throw new CommandNotReadException(tooLong);
            }

            if (lineFeed >= 0)
            {
                CountCommandBytes(lineLength);
                var line = buffer[start..lineFeed];
                start = lineFeed + 1;
                return line;
            }

            searched = end - start;
            await RequireBytesAsync(searched + 1, cancellationToken);
        }
    }

    private void ThrowIfPastUnauthenticatedLimit(long value, int limit, ReadOnlySpan<byte> detail)
    {
        if (AppliesUnauthenticatedLimits && value > limit)
        {
            throw new CommandNotReadException(RedisCommandReadOutcome.ProtocolError(detail));
        }
    }

    private void CountCommandBytes(long count)
    {
        commandBytes += count;

        if (commandBytes > maxCommandBytes)
        {
            throw new CommandNotReadException(RedisCommandReadOutcome.CommandTooLarge);
        }
    }

    /// <summary>Reads until at least <paramref name="count"/> unread bytes are buffered.</summary>
    private async ValueTask RequireBytesAsync(int count, CancellationToken cancellationToken)
    {
        while (end - start < count)
        {
            MakeRoom(count);
            var read = await connection.ReadAsync(buffer.AsMemory(end), cancellationToken);

            if (read == 0)
            {
                throw new CommandNotReadException(RedisCommandReadOutcome.ConnectionClosed);
            }

            end += read;
        }
    }

    private void MakeRoom(int count)
    {
        if (start > 0)
        {
            Buffer.BlockCopy(buffer, start, buffer, 0, end - start);
            end -= start;
            start = 0;
        }

        if (buffer.Length < count)
        {
            Array.Resize(ref buffer, (int)Math.Min(Math.Max(count, 2L * buffer.Length), Array.MaxLength));
        }
    }

    /// <summary>
    /// Reads the number of a <c>*</c> or <c>$</c> line ending in CR as Redis does: an optional
    /// minus sign, then <c>0</c> alone or digits not starting with <c>0</c>, within 64 bits.
    /// </summary>
    private static bool TryParseHeaderNumber(byte[] line, out long value)
    {
        value = 0;

        if (line.Length < 3 || line[^1] != (byte)'\r')
        {
            return false;
        }

        var number = line.AsSpan(1, line.Length - 2);
        var negative = number[0] == (byte)'-';
        var digits = negative ? number[1..] : number;
        var canonical = digits.Length > 0
            && digits.IndexOfAnyExceptInRange((byte)'0', (byte)'9') < 0
            && (digits[0] != (byte)'0' || (digits.Length == 1 && !negative));

        return canonical && long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Ends a read that found no command, carrying the outcome to return.</summary>
    private sealed class CommandNotReadException(RedisCommandReadOutcome outcome) : Exception
    {
        public RedisCommandReadOutcome Outcome { get; } = outcome;
    }
}
