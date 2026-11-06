using System.Text;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// One control line split into its operation, upper-cased so it matches case-insensitively, and
/// the rest of the line after the spaces or tabs that follow it (ADR-0113 decision 0).
/// </summary>
/// <param name="Operation">The operation name in upper case; empty for a blank line.</param>
/// <param name="Arguments">The rest of the line; empty when there is none.</param>
internal readonly record struct NatsControlLine(string Operation, string Arguments)
{
    private static readonly char[] FieldSeparators = [' ', '\t'];

    /// <summary>Splits a line read without its ending.</summary>
    public static NatsControlLine Parse(byte[] line)
    {
        var text = Encoding.UTF8.GetString(line).Trim(FieldSeparators);
        var split = text.IndexOfAny(FieldSeparators);
        return split < 0
            ? new NatsControlLine(text.ToUpperInvariant(), string.Empty)
            : new NatsControlLine(text[..split].ToUpperInvariant(), text[(split + 1)..].TrimStart(FieldSeparators));
    }
}
