namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// Where one step left an enhanced authentication (section 4.12).
/// </summary>
internal enum Mqtt5AuthenticationOutcome
{
    /// <summary>The method has a challenge for the client: <c>AUTH</c> <c>0x18</c> Continue authentication.</summary>
    Continue,

    /// <summary>The client is authenticated; the step's data is the method's last message.</summary>
    Accepted,

    /// <summary>The client is not authenticated.</summary>
    Refused,
}
