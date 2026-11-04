namespace Surl.Protocol.Tftp;

/// <summary>
/// One option of a request or an OACK (RFC 2347): its name and its value, both as text.
/// </summary>
/// <param name="Name">The option name; the server writes the names it accepts in lower case.</param>
/// <param name="Value">The option value.</param>
internal sealed record TftpOption(string Name, string Value);
