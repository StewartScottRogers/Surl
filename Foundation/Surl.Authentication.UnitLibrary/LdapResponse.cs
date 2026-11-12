namespace Surl.Authentication;

/// <summary>
/// One <c>LDAPMessage</c> from a directory, reduced to what <see cref="LdapAccountSource"/> reads
/// (RFC 4511 section 4.1.1).
/// </summary>
/// <param name="MessageId">The message ID.</param>
/// <param name="Kind">The kind of protocol operation.</param>
/// <param name="ResultCode">The <c>resultCode</c> of a result; 0 for an entry or a reference.</param>
/// <param name="EntryName">A search entry's <c>objectName</c>; <see langword="null"/> otherwise.</param>
internal sealed record LdapResponse(int MessageId, LdapResponseKind Kind, int ResultCode, string? EntryName);
