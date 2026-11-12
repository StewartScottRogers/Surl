using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Writes a fault plan's rule back as one <c>--fault</c> item (ADR-0124 decision 4's grammar), as
/// <c>GET /v1/faults</c> lists it: the kind, then each key the rule sets in the reader's key order, each value
/// percent-encoded where the item grammar would split it. <c>times</c> is what is left of the rule's count, so
/// posting the item again makes a rule that acts as this one still will.
/// </summary>
internal static class FaultItemText
{
    private static readonly FrozenDictionary<FaultKind, string> KindWords = new Dictionary<FaultKind, string>
    {
        [FaultKind.Delay] = "delay",
        [FaultKind.Drop] = "drop",
        [FaultKind.Error] = "error",
        [FaultKind.OutOfSpace] = "out-of-space",
        [FaultKind.RefuseLogin] = "refuse-login",
    }.ToFrozenDictionary();

    /// <summary>Writes <paramref name="rule"/> as one <c>--fault</c> item.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The item, such as <c>drop;scheme=http;times=1</c>.</returns>
    public static string Format(FaultRule rule)
    {
        var item = new StringBuilder(KindWords[rule.Kind]);
        Append(item, "listener", rule.Listener);
        Append(item, "scheme", rule.Scheme);
        Append(item, "account", rule.Account);
        Append(item, "path", rule.Path);
        Append(item, "command", rule.Command);
        Append(item, "stage", rule.Stage?.ToString().ToLowerInvariant());
        Append(item, "ms", Number(rule.DelayMilliseconds));
        Append(item, "after-bytes", Number(rule.AfterBytes));
        Append(item, "reply", rule.Reply);
        Append(item, "times", Number(rule.Times));
        return item.ToString();
    }

    private static string? Number(long? number) => number?.ToString(CultureInfo.InvariantCulture);

    private static void Append(StringBuilder item, string key, string? value)
    {
        if (value is not null)
        {
            item.Append(';').Append(key).Append('=').Append(Encode(value));
        }
    }

    // The item grammar splits at ';', ',' and '=' and decodes '%' (ADR-0101 decision 3), and a control character
    // may not stand in a field, so each is written as its UTF-8 bytes in %XX form.
    private static string Encode(string value)
    {
        var encoded = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is ';' or ',' or '=' or '%' || char.IsControl(character))
            {
                // Every character encoded is ASCII or a C1 control, one or two UTF-8 bytes.
                foreach (var b in Encoding.UTF8.GetBytes([character]))
                {
                    encoded.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
                }
            }
            else
            {
                encoded.Append(character);
            }
        }

        return encoded.ToString();
    }
}
