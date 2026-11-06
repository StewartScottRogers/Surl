using System.Text;
using Surl.HttpMessage;

namespace Surl.Protocol.HttpProxy;

/// <summary>
/// What an <c>ftp://</c> absolute-form <c>GET</c> asks the proxy to fetch over FTP (ADR-0088
/// decision 3 and Amendment 4): the login, the path, the transfer type and the transfer command.
/// </summary>
/// <param name="User">The name sent with <c>USER</c>.</param>
/// <param name="Password">The password sent with <c>PASS</c>.</param>
/// <param name="Path">The path percent-decoded, without its leading <c>/</c>; empty for the login directory.</param>
/// <param name="TransferType">The <c>TYPE</c> argument: <c>A</c> for <c>;type=a</c>, otherwise <c>I</c>.</param>
/// <param name="TransferCommand">
/// <c>RETR</c> for a file, <c>LIST</c> for a path that is empty or ends in <c>/</c>, <c>NLST</c>
/// for <c>;type=d</c> (RFC 1738 section 3.2.2).
/// </param>
internal sealed record FtpFetch(string User, string Password, string Path, string TransferType, string TransferCommand)
{
    /// <summary>The name a request that carries no credentials logs in with, as upstream curl does.</summary>
    public const string AnonymousUser = "anonymous";

    /// <summary>The password a request that carries no credentials logs in with, as upstream curl does.</summary>
    public const string AnonymousPassword = "ftp@example.com";

    private const string TypeParameter = ";type=";

    /// <summary>Whether the fetch is a directory listing rather than a file.</summary>
    public bool IsListing => TransferCommand != "RETR";

    /// <summary>The transfer command line sent on the control connection.</summary>
    public string TransferCommandLine => Path.Length == 0 ? TransferCommand : $"{TransferCommand} {Path}";

    /// <summary>
    /// Reads the fetch from an <c>ftp</c> target and the request's fields. The credentials are the
    /// target's userinfo, percent-decoded; else an <c>Authorization: Basic</c> field's; else
    /// <see cref="AnonymousUser"/> and <see cref="AnonymousPassword"/>. Upstream curl 8.21.0 sends
    /// both the userinfo and the field for <c>ftp://user:pass@host/</c> and for <c>-u</c>
    /// (measured, Fixtures/README.md).
    /// </summary>
    /// <param name="target">The <c>ftp</c> target.</param>
    /// <param name="head">The request head.</param>
    /// <returns>
    /// The fetch; <see langword="null"/> when a credential or the path holds a control character,
    /// which would end an FTP command line, or the <c>Basic</c> credentials are not Base64.
    /// </returns>
    public static FtpFetch? Of(ProxyTarget target, HttpRequestHead head)
    {
        var (path, type) = SplitType(target.OriginForm);
        var credentials = target.UserInfo is { } userInfo ? SplitCredentials(userInfo, decode: true) : BasicCredentials(head);
        if (credentials is not var (user, password))
        {
            return null;
        }

        var decodedPath = Uri.UnescapeDataString(path[1..]);
        var command = type == 'd' ? "NLST" : decodedPath.Length == 0 || decodedPath.EndsWith('/') ? "LIST" : "RETR";
        var fetch = new FtpFetch(user, password, decodedPath, type == 'a' ? "A" : "I", command);

        return $"{user}{password}{decodedPath}".Any(char.IsControl) ? null : fetch;
    }

    // ";type=a", ";type=i" or ";type=d" ending the path (RFC 1738 section 3.2.2); 'i' when none.
    private static (string Path, char Type) SplitType(string originForm)
    {
        var typeStart = originForm.Length - TypeParameter.Length - 1;
        var hasType = typeStart > 0 && originForm.AsSpan(typeStart, TypeParameter.Length).Equals(TypeParameter, StringComparison.OrdinalIgnoreCase);

        return hasType ? (originForm[..typeStart], char.ToLowerInvariant(originForm[^1])) : (originForm, 'i');
    }

    private static (string User, string Password)? BasicCredentials(HttpRequestHead head)
    {
        var basic = head.GetFieldValues("Authorization")
            .FirstOrDefault(value => value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase));
        if (basic is null)
        {
            return (AnonymousUser, AnonymousPassword);
        }

        var encoded = basic["Basic ".Length..].Trim();
        var bytes = new byte[encoded.Length];

        return Convert.TryFromBase64String(encoded, bytes, out var length)
            ? SplitCredentials(Encoding.UTF8.GetString(bytes, 0, length), decode: false)
            : null;
    }

    private static (string User, string Password) SplitCredentials(string credentials, bool decode)
    {
        var colon = credentials.IndexOf(':');
        var user = colon < 0 ? credentials : credentials[..colon];
        var password = colon < 0 ? string.Empty : credentials[(colon + 1)..];

        return decode ? (Uri.UnescapeDataString(user), Uri.UnescapeDataString(password)) : (user, password);
    }
}
