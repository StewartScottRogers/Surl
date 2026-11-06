using System.Text;
using System.Text.Json;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The login a CONNECT payload carries (SIO "Connection to a namespace"; ADR-0120, decision 4;
/// ADR-0100, decision 4): a string <c>token</c> is the empty-name account's password; otherwise a
/// string <c>username</c> and <c>password</c> are that account's login; otherwise there is none.
/// Other members are ignored.
/// </summary>
internal static class SocketIoConnectCredentials
{
    /// <summary>
    /// The <see cref="PasswordLogin"/> the payload carries, for the policy to judge.
    /// </summary>
    /// <param name="payload">The CONNECT's payload: empty, or a JSON object the reader already checked.</param>
    /// <param name="scheme">The listener's scheme.</param>
    /// <param name="tlsSession">The connection's encryption, or <see langword="null"/>.</param>
    public static PasswordLogin Read(ReadOnlyMemory<byte> payload, string scheme, TlsSession? tlsSession)
    {
        if (payload.IsEmpty)
        {
            return new PasswordLogin(scheme, null, null, tlsSession);
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (StringMember(root, "token") is { } token)
        {
            return new PasswordLogin(scheme, string.Empty, Encoding.UTF8.GetBytes(token), tlsSession);
        }

        if (StringMember(root, "username") is { } name && StringMember(root, "password") is { } password)
        {
            return new PasswordLogin(scheme, name, Encoding.UTF8.GetBytes(password), tlsSession);
        }

        return new PasswordLogin(scheme, null, null, tlsSession);
    }

    private static string? StringMember(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
