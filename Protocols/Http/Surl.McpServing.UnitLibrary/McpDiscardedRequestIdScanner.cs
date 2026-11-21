using System.Text.Json;

namespace Surl.McpServing;

/// <summary>
/// Finds the <c>id</c> of a message too long to keep (ADR-0103 amendment 1): fed the message's bytes in
/// any number of pieces, it tracks JSON nesting and string state and records the value of the member named
/// <c>id</c> at the top level of the message, wherever in the message it sits. It keeps at most
/// <see cref="MaximumIdBytes" /> bytes of the id's text, whatever the length of what it is fed.
/// </summary>
internal sealed class McpDiscardedRequestIdScanner
{
    /// <summary>The most bytes of an id's JSON text the scanner keeps; a longer id is left unread.</summary>
    internal const int MaximumIdBytes = 256;

    private static readonly byte[] IdName = "id"u8.ToArray();

    private readonly byte[] _idText = new byte[MaximumIdBytes];

    private State _state = State.Structure;

    private int _depth;

    private bool _expectingMemberName;

    private bool _escaped;

    private int _nameBytesMatched;

    private bool _idMemberSeen;

    private int _idTextLength;

    private bool _idTextTooLong;

    private bool _idComplete;

    private enum State
    {
        Structure,
        OtherString,
        MemberName,
        AfterIdName,
        BeforeIdValue,
        IdString,
        IdNumber,
    }

    /// <summary>Reads the next piece of the message.</summary>
    /// <param name="bytes">The message's next bytes.</param>
    internal void Feed(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            Step(value);
        }
    }

    /// <summary>Ends the message and forgets it, ready for the next.</summary>
    /// <returns>The id read, or <see langword="null" /> when the message had none a client could match.</returns>
    internal JsonRpcRequestId? Finish()
    {
        JsonRpcRequestId? id = _idComplete && !_idTextTooLong ? ParseId(_idText.AsMemory(0, _idTextLength)) : null;
        _state = State.Structure;
        _depth = 0;
        _expectingMemberName = false;
        _escaped = false;
        _idMemberSeen = false;
        _idTextLength = 0;
        _idTextTooLong = false;
        _idComplete = false;
        return id;
    }

    // Only a JSON string or number is an id; text that is neither, or malformed, is no id.
    private static JsonRpcRequestId? ParseId(ReadOnlyMemory<byte> text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            return new JsonRpcRequestId(document.RootElement.Clone());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void Step(byte value)
    {
        StepByState[(int)_state](this, value);
    }

    // Indexed by State, so a state's step is found without a branch per state.
    private static readonly Action<McpDiscardedRequestIdScanner, byte>[] StepByState =
    [
        static (scanner, value) => scanner.StepStructure(value),
        static (scanner, value) => scanner.StepOtherString(value),
        static (scanner, value) => scanner.StepMemberName(value),
        static (scanner, value) => scanner.StepAfterIdName(value),
        static (scanner, value) => scanner.StepBeforeIdValue(value),
        static (scanner, value) => scanner.StepIdString(value),
        static (scanner, value) => scanner.StepIdNumber(value),
    ];

    private void StepStructure(byte value)
    {
        if (value == (byte)'"')
        {
            BeginString();
        }
        else if (value is (byte)'{' or (byte)'[')
        {
            _depth++;
            _expectingMemberName = value == (byte)'{' && _depth == 1;
        }
        else if (value is (byte)'}' or (byte)']')
        {
            _depth--;
        }
        else if (value == (byte)',')
        {
            _expectingMemberName = _depth == 1;
        }
        else if (value == (byte)':')
        {
            _expectingMemberName = false;
        }
    }

    private void BeginString()
    {
        _escaped = false;
        _nameBytesMatched = 0;
        _state = _depth == 1 && _expectingMemberName ? State.MemberName : State.OtherString;
    }

    // Consumes one byte of a string; true when it was the closing quote.
    private bool EndsString(byte value)
    {
        if (_escaped)
        {
            _escaped = false;
            return false;
        }

        if (value == (byte)'\\')
        {
            _escaped = true;
            return false;
        }

        return value == (byte)'"';
    }

    private void StepOtherString(byte value)
    {
        if (EndsString(value))
        {
            _state = State.Structure;
        }
    }

    // A member name written with an escape never matches: no client writes "id" that way.
    private void StepMemberName(byte value)
    {
        bool escapedBefore = _escaped;
        if (EndsString(value))
        {
            EndMemberName();
            return;
        }

        bool matches = !escapedBefore && !_escaped && _nameBytesMatched < IdName.Length && IdName[_nameBytesMatched] == value;
        _nameBytesMatched = matches ? _nameBytesMatched + 1 : IdName.Length + 1;
    }

    // The first top-level id wins; a later one is skipped like any other member.
    private void EndMemberName()
    {
        bool isId = _nameBytesMatched == IdName.Length && !_idMemberSeen;
        _idMemberSeen |= isId;
        _state = isId ? State.AfterIdName : State.Structure;
    }

    private void StepAfterIdName(byte value)
    {
        if (value == (byte)':')
        {
            _expectingMemberName = false;
            _state = State.BeforeIdValue;
        }
        else if (!IsWhitespace(value))
        {
            _state = State.Structure;
            StepStructure(value);
        }
    }

    // An id that is neither a string nor a number (an object, an array, true, false, null) is left unread.
    private void StepBeforeIdValue(byte value)
    {
        if (IsWhitespace(value))
        {
            return;
        }

        if (value == (byte)'"')
        {
            _escaped = false;
            _state = State.IdString;
            KeepIdByte(value);
        }
        else if (value == (byte)'-' || value is >= (byte)'0' and <= (byte)'9')
        {
            _state = State.IdNumber;
            KeepIdByte(value);
        }
        else
        {
            _state = State.Structure;
            StepStructure(value);
        }
    }

    private void StepIdString(byte value)
    {
        KeepIdByte(value);
        if (EndsString(value))
        {
            _idComplete = true;
            _state = State.Structure;
        }
    }

    private void StepIdNumber(byte value)
    {
        if (IsWhitespace(value) || value is (byte)',' or (byte)'}' or (byte)']')
        {
            _idComplete = true;
            _state = State.Structure;
            StepStructure(value);
            return;
        }

        KeepIdByte(value);
    }

    private void KeepIdByte(byte value)
    {
        if (_idTextLength == MaximumIdBytes)
        {
            _idTextTooLong = true;
            return;
        }

        _idText[_idTextLength++] = value;
    }

    private static bool IsWhitespace(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
}
