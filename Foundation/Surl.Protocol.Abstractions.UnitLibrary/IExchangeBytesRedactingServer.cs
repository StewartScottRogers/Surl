namespace Surl.Protocol.Abstractions;

/// <summary>
/// Optional: implemented by a protocol server that receives secrets (a password, a token) so the
/// exchange log can write them as <c>****</c> (ADR-0182, decision 6). A server that does not
/// implement it is logged byte for byte.
/// </summary>
public interface IExchangeBytesRedactingServer
{
    /// <summary>
    /// Creates the redactor for one exchange.
    /// </summary>
    /// <param name="context">The exchange the redactor is for.</param>
    /// <returns>A redactor used by that exchange alone, never shared.</returns>
    IReceivedBytesRedactor CreateRedactor(ExchangeRedactionContext context);
}
