namespace Surl.Extended.Protocol.Amqp0;

/// <summary>
/// Matches a routing key against a topic exchange's binding key (section 3.1.3.3): words split at
/// <c>.</c>, <c>*</c> one word and <c>#</c> zero or more. Runs in time proportional to the product
/// of the two word counts, however many <c>#</c> the binding key holds.
/// </summary>
internal static class Amqp0TopicKeyMatcher
{
    /// <summary>Whether <paramref name="routingKey"/> matches <paramref name="bindingKey"/>.</summary>
    /// <param name="bindingKey">The binding key, with <c>*</c> and <c>#</c>.</param>
    /// <param name="routingKey">The publish's routing key.</param>
    /// <returns>Whether it matches.</returns>
    public static bool Matches(string bindingKey, string routingKey)
    {
        var key = routingKey.Split('.');
        var matched = new bool[key.Length + 1];
        matched[0] = true;
        foreach (var word in bindingKey.Split('.'))
        {
            matched = Advance(matched, word, key);
        }

        return matched[key.Length];
    }

    // matched[i]: the binding words so far match the first i routing words.
    private static bool[] Advance(bool[] matched, string word, string[] key)
    {
        var next = new bool[matched.Length];
        for (var index = 0; index < matched.Length; index++)
        {
            if (!matched[index])
            {
                continue;
            }

            if (word == "#")
            {
                Array.Fill(next, true, index, matched.Length - index);
                break;
            }

            if (index < key.Length && (word == "*" || word == key[index]))
            {
                next[index + 1] = true;
            }
        }

        return next;
    }
}
