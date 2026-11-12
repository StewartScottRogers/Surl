namespace Surl.Protocol.Abstractions;

/// <summary>
/// The exchange a login was made in: what a login request carries so the policy can name the exchange
/// and the scheme the login was really made over in its <see cref="LoginJudgedEvent"/> and match a
/// <c>refuse-login</c> rule scoped by listener (ADR-0128, amended by BL-871).
/// </summary>
/// <param name="ExchangeId">The exchange's <see cref="ExchangeContext.ExchangeId"/>.</param>
/// <param name="ListenUrl">The listen URL that accepted the exchange.</param>
public sealed record LoginExchange(long ExchangeId, ListenUrl ListenUrl)
{
    /// <summary>
    /// Gets the scheme the login was made over, the listen URL's: <c>sftp</c>, not <c>ssh</c>.
    /// </summary>
    public string Scheme => ListenUrl.Scheme;

    /// <summary>
    /// Gets the listen URL as the instance reports it, bound port included.
    /// </summary>
    public string ReportedListenUrl => Abstractions.ReportedListenUrl.Format(ListenUrl);

    /// <summary>
    /// Takes the exchange a server was handed.
    /// </summary>
    /// <param name="context">The exchange context.</param>
    /// <returns>What a login request carries.</returns>
    public static LoginExchange Of(ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new LoginExchange(context.ExchangeId, context.ListenUrl);
    }
}
