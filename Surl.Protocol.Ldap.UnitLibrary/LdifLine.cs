namespace Surl.Protocol.Ldap;

/// <summary>
/// One line of an LDIF file: a physical line, or a logical one with its continuation lines
/// unfolded into it.
/// </summary>
/// <param name="Number">The one-based number of the line's first physical line.</param>
/// <param name="Text">The line's text, without its line ending.</param>
internal readonly record struct LdifLine(int Number, string Text);
