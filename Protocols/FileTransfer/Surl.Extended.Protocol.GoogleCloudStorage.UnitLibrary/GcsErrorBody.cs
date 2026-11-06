using System.Text;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Writes the XML API's <c>Error</c> body ("HTTP status and error codes"; ADR-0110, decision 1.3):
/// the single-quoted XML declaration, then <c>Code</c> and <c>Message</c>, on one line, as UTF-8.
/// No client byte is echoed.
/// </summary>
internal static class GcsErrorBody
{
    /// <summary>
    /// The XML declaration every XML body the server writes starts with.
    /// </summary>
    public const string Declaration = "<?xml version='1.0' encoding='UTF-8'?>";

    /// <summary>
    /// Renders the body.
    /// </summary>
    /// <param name="error">The error, whose code and message are fixed text.</param>
    /// <returns>The body's bytes.</returns>
    public static byte[] Render(GcsError error) =>
        Encoding.UTF8.GetBytes($"{Declaration}<Error><Code>{error.Code}</Code><Message>{error.Message}</Message></Error>");
}
