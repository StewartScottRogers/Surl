namespace Surl.Authentication;

/// <summary>
/// How <see cref="LdapAccountSource"/> reaches and searches a directory (ADR-0149, decision 3).
/// </summary>
/// <param name="Url">The directory's URL.</param>
public sealed class LdapAccountSourceSettings(LdapDirectoryUrl Url)
{
    /// <summary>The seconds one login's whole directory check may take (decision 10): 1 to 60.</summary>
    public const int DefaultTimeoutSeconds = 5;

    /// <summary>The directory's URL.</summary>
    public LdapDirectoryUrl Url { get; } = Url ?? throw new ArgumentNullException(nameof(Url));

    /// <summary>The DN <c>surl</c> binds as before searching; <see langword="null"/> searches anonymously.</summary>
    public string? BindDn { get; init; }

    /// <summary>The password of <see cref="BindDn"/>, as bytes; never written to a note or a message.</summary>
    public ReadOnlyMemory<byte> BindPassword { get; init; }

    /// <summary>The seconds one login's directory check may take.</summary>
    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;

    /// <summary>The schemes a directory account may log in on; <see langword="null"/> means every scheme.</summary>
    public IReadOnlyList<string>? Protocols { get; init; }

    /// <summary>Whether <c>ldap://</c> is used without StartTLS (<c>;plaintext</c>).</summary>
    public bool IsPlaintext { get; init; }
}
