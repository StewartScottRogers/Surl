using System.Buffers.Binary;
using System.Text;
using System.Text.Unicode;

namespace Surl.Extended.Protocol.SignalR;

/// <summary>
/// Reads the subset of MessagePack the hub protocol needs from one buffer, front to back
/// (MessagePack specification, "Formats"; HubProtocol.md "MessagePack (MsgPack) encoding"). Every read
/// returns <see langword="false"/> rather than throwing on bytes that are not what it expects, and
/// skipping a value never recurses, so a deeply nested value cannot exhaust the stack.
/// </summary>
/// <param name="data">The bytes to read.</param>
internal sealed class MessagePackCursor(ReadOnlyMemory<byte> data)
{
    private int position;

    /// <summary>
    /// Whether every byte has been read.
    /// </summary>
    public bool IsAtEnd => position == data.Length;

    /// <summary>
    /// Reads the next value's header: its first byte and the length or value bytes that follow it.
    /// </summary>
    /// <param name="token">The header read.</param>
    /// <returns><see langword="false"/> at the end of the data, on the unused byte <c>0xc1</c>, or when an entry count passes the bytes left.</returns>
    public bool TryReadToken(out MessagePackToken token)
    {
        token = default;
        if (!TryReadUnsigned(1, out var first))
        {
            return false;
        }

        var (kind, width, isSigned, fixedNumber) = Describe((byte)first);
        var number = fixedNumber;
        if (width > 0 && !TryReadNumber(width, isSigned, kind == MessagePackKind.Int, out number))
        {
            return false;
        }

        // An extension's type byte is counted with its payload.
        number += kind == MessagePackKind.Ext ? 1 : 0;
        var isCountPastData = kind is MessagePackKind.Array or MessagePackKind.Map && number > data.Length - position;
        token = new MessagePackToken(kind, number);

        return kind != MessagePackKind.Invalid && !isCountPastData;
    }

    /// <summary>
    /// Whether the next value is of <paramref name="expected"/> kind, without reading it.
    /// </summary>
    /// <param name="expected">The kind wanted.</param>
    /// <returns>Whether the next value's header is readable and of that kind.</returns>
    public bool IsNext(MessagePackKind expected)
    {
        var start = position;
        var isRead = TryReadToken(out var token);
        position = start;

        return isRead && token.Kind == expected;
    }

    /// <summary>
    /// Reads the next value's bytes without interpreting them, whatever it holds.
    /// </summary>
    /// <param name="value">The value's bytes, header included, as they stood in the data.</param>
    /// <returns><see langword="false"/> when the value is cut short or malformed.</returns>
    public bool TrySkipValue(out ReadOnlyMemory<byte> value)
    {
        var start = position;
        value = default;
        long pending = 1;
        while (pending > 0)
        {
            if (!TryReadToken(out var token) || !TrySkipPayload(token))
            {
                return false;
            }

            pending += Children(token) - 1;
        }

        value = data.Slice(start, position - start);

        return true;
    }

    /// <summary>
    /// Reads an array's header.
    /// </summary>
    /// <param name="count">How many values follow.</param>
    /// <returns>Whether the next value is an array.</returns>
    public bool TryReadArrayCount(out int count)
    {
        var isArray = TryReadToken(out var token) && token.Kind == MessagePackKind.Array;
        count = isArray ? (int)token.Number : 0;

        return isArray;
    }

    /// <summary>
    /// Reads an integer that fits an <see cref="int"/>.
    /// </summary>
    /// <param name="number">The integer.</param>
    /// <returns>Whether the next value is such an integer.</returns>
    public bool TryReadInt(out int number)
    {
        var isInt = TryReadToken(out var token) && token.Kind == MessagePackKind.Int && token.Number is >= int.MinValue and <= int.MaxValue;
        number = isInt ? (int)token.Number : 0;

        return isInt;
    }

    /// <summary>
    /// Reads a boolean.
    /// </summary>
    /// <param name="value">The boolean.</param>
    /// <returns>Whether the next value is a boolean.</returns>
    public bool TryReadBool(out bool value)
    {
        var isBool = TryReadToken(out var token) && token.Kind == MessagePackKind.Bool;
        value = isBool && token.Number == 1;

        return isBool;
    }

    /// <summary>
    /// Reads a string, or nil as <see langword="null"/> when <paramref name="allowNil"/>.
    /// </summary>
    /// <param name="allowNil">Whether nil is accepted.</param>
    /// <param name="value">The string, or <see langword="null"/> for nil.</param>
    /// <returns>Whether the next value is a well-formed UTF-8 string, or an accepted nil.</returns>
    public bool TryReadString(bool allowNil, out string? value)
    {
        value = null;
        if (!TryReadToken(out var token))
        {
            return false;
        }

        if (token.Kind == MessagePackKind.Nil)
        {
            return allowNil;
        }

        if (token.Kind != MessagePackKind.Str || !TryTake(token.Number, out var payload) || !Utf8.IsValid(payload.Span))
        {
            return false;
        }

        value = Encoding.UTF8.GetString(payload.Span);

        return true;
    }

    // The forms of first bytes 0xc0 to 0xdf, in order: what the value is, how many bytes of length or
    // value follow, whether those bytes are a signed integer, and the number a form with no such bytes has.
    private static readonly (MessagePackKind Kind, int Width, bool IsSigned, long Fixed)[] WideForms =
    [
        (MessagePackKind.Nil, 0, false, 0),
        (MessagePackKind.Invalid, 0, false, 0),
        (MessagePackKind.Bool, 0, false, 0),
        (MessagePackKind.Bool, 0, false, 1),
        (MessagePackKind.Bin, 1, false, 0),
        (MessagePackKind.Bin, 2, false, 0),
        (MessagePackKind.Bin, 4, false, 0),
        (MessagePackKind.Ext, 1, false, 0),
        (MessagePackKind.Ext, 2, false, 0),
        (MessagePackKind.Ext, 4, false, 0),
        (MessagePackKind.Float, 0, false, 4),
        (MessagePackKind.Float, 0, false, 8),
        (MessagePackKind.Int, 1, false, 0),
        (MessagePackKind.Int, 2, false, 0),
        (MessagePackKind.Int, 4, false, 0),
        (MessagePackKind.Int, 8, false, 0),
        (MessagePackKind.Int, 1, true, 0),
        (MessagePackKind.Int, 2, true, 0),
        (MessagePackKind.Int, 4, true, 0),
        (MessagePackKind.Int, 8, true, 0),
        (MessagePackKind.Ext, 0, false, 1),
        (MessagePackKind.Ext, 0, false, 2),
        (MessagePackKind.Ext, 0, false, 4),
        (MessagePackKind.Ext, 0, false, 8),
        (MessagePackKind.Ext, 0, false, 16),
        (MessagePackKind.Str, 1, false, 0),
        (MessagePackKind.Str, 2, false, 0),
        (MessagePackKind.Str, 4, false, 0),
        (MessagePackKind.Array, 2, false, 0),
        (MessagePackKind.Array, 4, false, 0),
        (MessagePackKind.Map, 2, false, 0),
        (MessagePackKind.Map, 4, false, 0),
    ];

    private static (MessagePackKind Kind, int Width, bool IsSigned, long Fixed) Describe(byte first)
    {
        if (first <= 0x7f)
        {
            return (MessagePackKind.Int, 0, false, first);
        }

        if (first <= 0x8f)
        {
            return (MessagePackKind.Map, 0, false, first & 0x0f);
        }

        if (first <= 0x9f)
        {
            return (MessagePackKind.Array, 0, false, first & 0x0f);
        }

        if (first <= 0xbf)
        {
            return (MessagePackKind.Str, 0, false, first & 0x1f);
        }

        return first <= 0xdf ? WideForms[first - 0xc0] : (MessagePackKind.Int, 0, false, (sbyte)first);
    }

    private static long Children(MessagePackToken token) => token.Kind switch
    {
        MessagePackKind.Array => token.Number,
        MessagePackKind.Map => token.Number * 2,
        _ => 0,
    };

    private bool TrySkipPayload(MessagePackToken token) =>
        token.Kind is not (MessagePackKind.Str or MessagePackKind.Bin or MessagePackKind.Ext or MessagePackKind.Float)
        || TryTake(token.Number, out _);

    private bool TryReadNumber(int width, bool isSigned, bool isInteger, out long number)
    {
        number = 0;
        if (!TryReadUnsigned(width, out var raw))
        {
            return false;
        }

        var unusedBits = 64 - (8 * width);
        number = (isSigned, isInteger) switch
        {
            (true, _) => (long)(raw << unusedBits) >> unusedBits,
            (false, true) => (long)Math.Min(raw, long.MaxValue),
            _ => (long)raw,
        };

        return true;
    }

    private bool TryReadUnsigned(int width, out ulong value)
    {
        value = 0;
        if (!TryTake(width, out var bytes))
        {
            return false;
        }

        value = width switch
        {
            1 => bytes.Span[0],
            2 => BinaryPrimitives.ReadUInt16BigEndian(bytes.Span),
            4 => BinaryPrimitives.ReadUInt32BigEndian(bytes.Span),
            _ => BinaryPrimitives.ReadUInt64BigEndian(bytes.Span),
        };

        return true;
    }

    private bool TryTake(long count, out ReadOnlyMemory<byte> bytes)
    {
        bytes = default;
        if (count > data.Length - position)
        {
            return false;
        }

        bytes = data.Slice(position, (int)count);
        position += (int)count;

        return true;
    }
}
