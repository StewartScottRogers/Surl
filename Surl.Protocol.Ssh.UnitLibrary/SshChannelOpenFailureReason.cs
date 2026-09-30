namespace Surl.Protocol.Ssh;

/// <summary>
/// The reason codes of <c>SSH_MSG_CHANNEL_OPEN_FAILURE</c> (RFC 4254, section 5.1) the server
/// sends (ADR-0051, decision 9).
/// </summary>
internal enum SshChannelOpenFailureReason : uint
{
    /// <summary><c>SSH_OPEN_ADMINISTRATIVELY_PROHIBITED</c>: <c>direct-tcpip</c>, <c>forwarded-tcpip</c> and <c>x11</c>.</summary>
    AdministrativelyProhibited = 1,

    /// <summary><c>SSH_OPEN_UNKNOWN_CHANNEL_TYPE</c>: every other type but <c>session</c>.</summary>
    UnknownChannelType = 3,

    /// <summary><c>SSH_OPEN_RESOURCE_SHORTAGE</c>: a <c>session</c> past the channel limit.</summary>
    ResourceShortage = 4,
}
