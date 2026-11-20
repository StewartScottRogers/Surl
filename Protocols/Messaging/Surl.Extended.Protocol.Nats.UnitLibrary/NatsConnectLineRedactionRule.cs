using System.Buffers;
using System.Text.Json;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// The <see cref="ILineRedactionRule"/> of the NATS client's lines: in a <c>CONNECT</c> line it
/// writes the value of the JSON fields <c>pass</c> and <c>auth_token</c> as
/// <see cref="LineBytesRedactor.SecretMask"/> inside their own quotes, and logs every other byte,
/// <c>user</c> included, as received (ADR-0182, decisions 1 and 6).
/// </summary>
/// <remarks>
/// Fields are found as <see cref="NatsConnectOptions"/> reads them: top-level names, matched
/// without regard to case and after JSON unescaping. A secret that is not a JSON string is written
/// as <c>"****"</c>. A <c>CONNECT</c> argument that is not one readable JSON object may still hold
/// a secret, so all of it is written as <c>****</c>. A line that is not <c>CONNECT</c>, or has no
/// argument, is logged as received.
/// </remarks>
internal sealed class NatsConnectLineRedactionRule : ILineRedactionRule
{
    private static ReadOnlySpan<byte> Operation => "CONNECT"u8;

    private static ReadOnlySpan<byte> QuotedMask => "\"****\""u8;

    /// <inheritdoc/>
    public void Redact(ReadOnlySpan<byte> line, ArrayBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var start = ArgumentStart(line);
        if (start < 0)
        {
            output.Write(line);
            return;
        }

        output.Write(line[..start]);
        var redacted = new ArrayBufferWriter<byte>();
        try
        {
            RedactObject(line[start..], redacted);
            output.Write(redacted.WrittenSpan);
        }
        catch (JsonException)
        {
            output.Write(LineBytesRedactor.SecretMask);
        }
    }

    /// <inheritdoc/>
    public void ObserveSent(ReadOnlySpan<byte> sent)
    {
        // NATS needs no knowledge of what the server sent: the secrets are always in CONNECT's JSON.
    }

    // Where CONNECT's argument starts, or -1 when the line is not a CONNECT with an argument.
    private static int ArgumentStart(ReadOnlySpan<byte> line)
    {
        var index = SkipBlanks(line, 0);
        var operationEnd = index + Operation.Length;
        if (line.Length <= operationEnd
            || !IsOperationIgnoringCase(line[index..operationEnd])
            || line[operationEnd] is not ((byte)' ' or (byte)'\t'))
        {
            return -1;
        }

        var start = SkipBlanks(line, operationEnd);
        return start < line.Length ? start : -1;
    }

    private static bool IsOperationIgnoringCase(ReadOnlySpan<byte> word)
    {
        for (var index = 0; index < word.Length; index++)
        {
            if ((word[index] | 0x20) != (Operation[index] | 0x20))
            {
                return false;
            }
        }

        return true;
    }

    private static int SkipBlanks(ReadOnlySpan<byte> line, int index)
    {
        while (index < line.Length && line[index] is (byte)' ' or (byte)'\t')
        {
            index++;
        }

        return index;
    }

    private static void RedactObject(ReadOnlySpan<byte> json, ArrayBufferWriter<byte> output)
    {
        var reader = new Utf8JsonReader(json);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("CONNECT's argument is not a JSON object");
        }

        var copied = 0;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isSecret = IsSecretName(reader.GetString());
            _ = reader.Read();
            if (isSecret)
            {
                copied = MaskValue(ref reader, json, copied, output);
            }
            else
            {
                reader.Skip();
            }
        }

        output.Write(json[copied..]);
    }

    private static bool IsSecretName(string? name) =>
        string.Equals(name, "pass", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "auth_token", StringComparison.OrdinalIgnoreCase);

    // Writes json from copied up to the value and its mask; returns where the rest begins.
    private static int MaskValue(ref Utf8JsonReader reader, ReadOnlySpan<byte> json, int copied, ArrayBufferWriter<byte> output)
    {
        var start = (int)reader.TokenStartIndex;
        if (reader.TokenType == JsonTokenType.String)
        {
            output.Write(json[copied..(start + 1)]);
            output.Write(LineBytesRedactor.SecretMask);
            return (int)reader.BytesConsumed - 1;
        }

        reader.Skip();
        output.Write(json[copied..start]);
        output.Write(QuotedMask);
        return (int)reader.BytesConsumed;
    }
}
