namespace Surl.Networking;

/// <summary>
/// One PEM block, or a DER key given the PEM label its structure matches: the label and the
/// decoded bytes.
/// </summary>
/// <param name="Label">The label, such as <c>CERTIFICATE</c> or <c>PRIVATE KEY</c>.</param>
/// <param name="Der">The decoded contents.</param>
internal sealed record PemBlock(string Label, byte[] Der);
