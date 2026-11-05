namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// An element of the <c>Error</c> body after <c>Message</c>: its name and its text, which is
/// either the server's own fixed text or a client value echoed (ADR-0109, decision 5.1).
/// </summary>
/// <param name="Name">The element's name, such as <c>HeaderName</c>.</param>
/// <param name="Text">The element's text before escaping.</param>
/// <param name="IsClientValue">Whether <paramref name="Text"/> is a client value, cut and escaped as one.</param>
internal sealed record AzureErrorElement(string Name, string Text, bool IsClientValue)
{
    /// <summary>
    /// An element holding the server's own text.
    /// </summary>
    /// <param name="name">The element's name.</param>
    /// <param name="text">The fixed text.</param>
    /// <returns>The element.</returns>
    public static AzureErrorElement Fixed(string name, string text) => new(name, text, false);

    /// <summary>
    /// An element holding a client value.
    /// </summary>
    /// <param name="name">The element's name.</param>
    /// <param name="value">The client's value.</param>
    /// <returns>The element.</returns>
    public static AzureErrorElement Echoed(string name, string value) => new(name, value, true);
}
