using System.Text.Json;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// Follows a binary packet's JSON payload token by token and checks its attachment placeholders: every
/// object with a <c>_placeholder</c> member must be exactly <c>{"_placeholder":true,"num":N}</c>, in either
/// member order, with <c>N</c> an integer from 0 to the attachment count less one, and each <c>N</c> used
/// exactly once (SIO "Packet encoding"; ADR-0120, decision 6).
/// </summary>
/// <remarks>
/// With an attachment count of 0 - a packet that is not binary - every token is taken without a check,
/// since only a binary packet has attachments.
/// </remarks>
/// <param name="attachmentCount">The packet's attachment count.</param>
internal sealed class SocketIoPlaceholderCheck(int attachmentCount)
{
    private readonly bool[] used = new bool[attachmentCount];
    private readonly Stack<Container> containers = new();
    private bool broken;

    /// <summary>Takes the token <paramref name="reader"/> is on.</summary>
    /// <param name="reader">The reader, on the token just read.</param>
    public void Take(ref Utf8JsonReader reader)
    {
        if (attachmentCount == 0)
        {
            return;
        }

        if (reader.TokenType == JsonTokenType.PropertyName)
        {
            containers.Peek().BeginMember(ref reader);
            return;
        }

        if (containers.TryPeek(out var parent))
        {
            parent.TakeValue(ref reader);
        }

        TakeStructure(reader.TokenType);
    }

    /// <summary>Says whether every placeholder was well formed and each attachment number used exactly once.</summary>
    /// <returns><see langword="true"/> when the placeholders are good.</returns>
    public bool EachUsedOnce() => !broken && Array.TrueForAll(used, isUsed => isUsed);

    private void TakeStructure(JsonTokenType token)
    {
        if (token is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            containers.Push(new Container());
        }
        else if (token is JsonTokenType.EndObject or JsonTokenType.EndArray)
        {
            End(containers.Pop());
        }
    }

    private void End(Container container)
    {
        if (!container.HasPlaceholder)
        {
            return;
        }

        var number = container.Number;
        if (!container.IsPlaceholder || number < 0 || number >= used.Length || used[number])
        {
            broken = true;
            return;
        }

        used[number] = true;
    }

    private enum PendingMember
    {
        None,
        Placeholder,
        Number,
    }

    private sealed class Container
    {
        private PendingMember pending;
        private int members;
        private bool placeholderTrue;

        public bool HasPlaceholder { get; private set; }

        public int Number { get; private set; } = -1;

        public bool IsPlaceholder => placeholderTrue && members == 2;

        public void BeginMember(ref Utf8JsonReader reader)
        {
            members++;
            pending = PendingMember.None;
            if (reader.ValueTextEquals("_placeholder"u8))
            {
                HasPlaceholder = true;
                pending = PendingMember.Placeholder;
            }
            else if (reader.ValueTextEquals("num"u8))
            {
                pending = PendingMember.Number;
            }
        }

        public void TakeValue(ref Utf8JsonReader reader)
        {
            if (pending == PendingMember.Placeholder)
            {
                placeholderTrue = reader.TokenType == JsonTokenType.True;
            }
            else if (pending == PendingMember.Number)
            {
                Number = reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number) ? number : -1;
            }

            pending = PendingMember.None;
        }
    }
}
