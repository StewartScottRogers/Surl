using System.Text;

namespace Surl.Kerberos;

/// <summary>
/// A Kerberos principal name: its realm and its name components (RFC 4120 section 6.2), such as
/// the realm <c>EXAMPLE.COM</c> and the components <c>HTTP</c> and <c>www.example.com</c>. Two
/// names are equal when their realms and components are equal, ordinally.
/// </summary>
/// <param name="Realm">The realm.</param>
/// <param name="Components">The name components, at least one.</param>
public sealed record KerberosPrincipalName(string Realm, IReadOnlyList<string> Components)
{
    /// <summary>Compares two names ordinally, realm and every component.</summary>
    /// <param name="other">The other name.</param>
    /// <returns><see langword="true" /> when both name the same principal.</returns>
    public bool Equals(KerberosPrincipalName? other) =>
        other is not null
        && string.Equals(Realm, other.Realm, StringComparison.Ordinal)
        && Components.SequenceEqual(other.Components, StringComparer.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(Realm, StringComparer.Ordinal);
        foreach (string component in Components)
        {
            hash.Add(component, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// The RFC 1964 section 2.1.1 display form (ADR-0057 decision 10): the components joined with
    /// <c>/</c>, then <c>@</c> and the realm, with every <c>/</c>, <c>@</c> and <c>\</c> inside a
    /// component or the realm escaped by <c>\</c>, as in <c>host/web01@EXAMPLE.COM</c>.
    /// </summary>
    /// <returns>The display form.</returns>
    public override string ToString()
    {
        StringBuilder text = new();
        for (int index = 0; index < Components.Count; index++)
        {
            if (index > 0)
            {
                text.Append('/');
            }

            AppendEscaped(text, Components[index]);
        }

        text.Append('@');
        AppendEscaped(text, Realm);
        return text.ToString();
    }

    private static void AppendEscaped(StringBuilder text, string name)
    {
        foreach (char character in name)
        {
            if (character is '/' or '@' or '\\')
            {
                text.Append('\\');
            }

            text.Append(character);
        }
    }
}
