namespace Surl.Authentication;

/// <summary>
/// Where an <see cref="ILdapDirectoryConnector"/> connects (ADR-0149, decision 11).
/// </summary>
/// <param name="Host">The directory's host name or IP literal.</param>
/// <param name="Port">The TCP port.</param>
/// <param name="UseTls">
/// <see langword="true"/> for TLS from the first byte (<c>ldaps</c>); <see langword="false"/> for a clear
/// connection, on which StartTLS is the caller's to ask.
/// </param>
public sealed record LdapDirectoryEndpoint(string Host, int Port, bool UseTls);
