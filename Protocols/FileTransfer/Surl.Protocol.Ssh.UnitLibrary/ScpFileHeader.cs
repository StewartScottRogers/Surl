namespace Surl.Protocol.Ssh;

/// <summary>
/// An SCP <c>C</c> line as a sink reads it: <c>C&lt;mode&gt; &lt;size&gt; &lt;name&gt;</c>
/// (ADR-0054, decision 4).
/// </summary>
/// <param name="Mode">The mode's octal digits as sent; never kept, since the store has no permission model.</param>
/// <param name="Size">How many bytes of the file follow.</param>
/// <param name="Name">The file's name, one path segment.</param>
internal sealed record ScpFileHeader(string Mode, long Size, string Name);
