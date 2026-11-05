namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// How a <c>CONNECT</c>'s login ended: by its user name and password, or by enhanced authentication.
/// </summary>
/// <param name="ReasonCode">The <c>CONNACK</c> Reason Code: <c>0x00</c> when accepted.</param>
/// <param name="Statement">The rule a refusal follows, for the log.</param>
/// <param name="Account">The account the login matched; <see langword="null"/> when refused or accepted unchecked.</param>
/// <param name="AuthenticationMethod">The Authentication Method used; <see langword="null"/> for a user name and password.</param>
/// <param name="AuthenticationData">The method's last message, which the <c>CONNACK</c> carries; empty for none.</param>
internal sealed record Mqtt5Login(
    Mqtt5ReasonCode ReasonCode,
    string Statement,
    string? Account,
    string? AuthenticationMethod = null,
    byte[]? AuthenticationData = null)
{
    /// <summary>
    /// A refused login.
    /// </summary>
    /// <param name="reasonCode">The <c>CONNACK</c> Reason Code.</param>
    /// <param name="statement">The rule the refusal follows.</param>
    /// <returns>The login.</returns>
    public static Mqtt5Login Refused(Mqtt5ReasonCode reasonCode, string statement) => new(reasonCode, statement, null);
}
