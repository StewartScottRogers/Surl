using System.Text;
using System.Text.Json;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats;

/// <summary>
/// CONNECT's JSON object (the protocol page, "CONNECT"; ADR-0113 decision 3). Field names match
/// case-insensitively, as the NATS server's own JSON reading does; unknown fields and fields
/// given as <c>null</c> are ignored; a known field of another JSON type makes the whole object
/// unreadable.
/// </summary>
internal sealed class NatsConnectOptions
{
    private static readonly Dictionary<string, Action<NatsConnectOptions, bool>> BooleanFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["verbose"] = (options, value) => options.Verbose = value,
            ["pedantic"] = (options, value) => options.Pedantic = value,
            ["tls_required"] = (options, value) => options.TlsRequired = value,
            ["echo"] = (options, value) => options.Echo = value,
            ["headers"] = (options, value) => options.Headers = value,
            ["no_responders"] = (options, value) => options.NoResponders = value,
        };

    private static readonly Dictionary<string, Action<NatsConnectOptions, string>> StringFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = (options, value) => options.Name = value,
            ["lang"] = (options, value) => options.Lang = value,
            ["version"] = (options, value) => options.Version = value,
            ["user"] = (options, value) => options.User = value,
            ["pass"] = (options, value) => options.Pass = value,
            ["auth_token"] = (options, value) => options.AuthToken = value,
            ["nkey"] = (options, value) => options.NKey = value,
            ["sig"] = (options, value) => options.Signature = value,
            ["jwt"] = (options, value) => options.Jwt = value,
        };

    public bool Verbose { get; private set; }

    public bool Pedantic { get; private set; }

    public bool TlsRequired { get; private set; }

    public bool Echo { get; private set; } = true;

    public bool Headers { get; private set; }

    public bool NoResponders { get; private set; }

    public string? Name { get; private set; }

    public string? Lang { get; private set; }

    public string? Version { get; private set; }

    public string? User { get; private set; }

    public string? Pass { get; private set; }

    public string? AuthToken { get; private set; }

    public string? NKey { get; private set; }

    public string? Signature { get; private set; }

    public string? Jwt { get; private set; }

    public long Protocol { get; private set; }

    /// <summary>Whether the client tried an NKey or JWT login, which surl refuses (ADR-0113 decision 2).</summary>
    public bool UsesKeys => NKey is not null || Signature is not null || Jwt is not null;

    /// <summary>
    /// Reads CONNECT's argument; null when it is not one JSON object or a known field has the
    /// wrong type.
    /// </summary>
    public static NatsConnectOptions? Parse(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        try
        {
            return ReadObject(ref reader);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The login CONNECT asks for: <c>user</c> and <c>pass</c> when <c>user</c> is given, else
    /// <c>auth_token</c> as the password of the account with an empty name, else no credentials
    /// (ADR-0113 decision 2; ADR-0100 decision 4).
    /// </summary>
    public PasswordLogin ToPasswordLogin(string scheme, TlsSession? tlsSession) =>
        User is not null ? new PasswordLogin(scheme, User, Encode(Pass), tlsSession)
        : AuthToken is not null ? new PasswordLogin(scheme, string.Empty, Encode(AuthToken), tlsSession)
        : new PasswordLogin(scheme, null, null, tlsSession);

    private static ReadOnlyMemory<byte>? Encode(string? text) =>
        text is null ? null : (ReadOnlyMemory<byte>?)Encoding.UTF8.GetBytes(text);

    private static NatsConnectOptions? ReadObject(ref Utf8JsonReader reader)
    {
        // Each Read either moves on or throws: the whole line is the final block, so an
        // incomplete object, or anything after it, is a JsonException.
        _ = reader.Read();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        var options = new NatsConnectOptions();
        _ = reader.Read();
        while (reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString()!;
            _ = reader.Read();
            if (!options.TrySet(name, ref reader))
            {
                return null;
            }

            _ = reader.Read();
        }

        _ = reader.Read();
        return options;
    }

    private bool TrySet(string name, ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return true;
        }

        if (BooleanFields.TryGetValue(name, out var setBoolean))
        {
            return TrySetBoolean(setBoolean, ref reader);
        }

        if (StringFields.TryGetValue(name, out var setString))
        {
            return TrySetString(setString, ref reader);
        }

        if (string.Equals(name, "protocol", StringComparison.OrdinalIgnoreCase))
        {
            return TrySetProtocol(ref reader);
        }

        reader.Skip();
        return true;
    }

    private bool TrySetBoolean(Action<NatsConnectOptions, bool> set, ref Utf8JsonReader reader)
    {
        var isBoolean = reader.TokenType is JsonTokenType.True or JsonTokenType.False;
        if (isBoolean)
        {
            set(this, reader.GetBoolean());
        }

        return isBoolean;
    }

    private bool TrySetString(Action<NatsConnectOptions, string> set, ref Utf8JsonReader reader)
    {
        var isString = reader.TokenType == JsonTokenType.String;
        if (isString)
        {
            set(this, reader.GetString()!);
        }

        return isString;
    }

    private bool TrySetProtocol(ref Utf8JsonReader reader)
    {
        var isInteger = reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out _);
        if (isInteger)
        {
            Protocol = reader.GetInt64();
        }

        return isInteger;
    }
}
