namespace Surl.Cli;

/// <summary>
/// One <c>--share &lt;name&gt;=&lt;path&gt;[;readonly][;protocols=&lt;scheme&gt;,...]</c> (ADR-0101 decision 4,
/// ADR-0102 decision 2.1): a top-level folder of the content store whose files live at <see cref="Path"/> on
/// disk. The record holds what was written, percent-decoded. <c>Surl.Composition</c> (BL-988) checks that the
/// name is one valid path segment, opens the path and expands each scheme in <see cref="Protocols"/> to its TLS
/// twin (ADR-0102 decision 2.1: <c>gopher</c> also admits <c>gophers</c>); the model holds only the schemes written.
/// </summary>
/// <param name="Name">The share's name, never empty, never holding a control character.</param>
/// <param name="Path">The directory on disk as written, never empty, never holding U+0000.</param>
/// <param name="IsReadOnly">Whether <c>readonly</c> was given, so every write in the share is refused.</param>
/// <param name="Protocols">The schemes written in <c>protocols=</c>, lower-cased, each once, TLS twins not added; null when not restricted.</param>
public sealed record ConfiguredShare(string Name, string Path, bool IsReadOnly, IReadOnlyList<string>? Protocols);
