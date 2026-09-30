namespace Surl.Protocol.Abstractions;

/// <summary>
/// One SASL exchange, from <see cref="IMailAuthenticationPolicy.StartSaslExchange"/> (ADR-0049,
/// section 6). The server calls <see cref="BeginAsync"/> once, then <see cref="ContinueAsync"/>
/// with each response while the last step was a <see cref="MailLoginOutcome.Challenge"/>. A client
/// that cancels with <c>*</c> drops the exchange without calling it again. It holds no unmanaged
/// resource, so it is not disposable.
/// </summary>
public interface ISaslExchange
{
    /// <summary>
    /// The first step: judges the initial response when one was sent, or issues the first challenge.
    /// </summary>
    /// <param name="cancellationToken">Cancels the step.</param>
    /// <returns>The step: a challenge to send, or how the login ended.</returns>
    ValueTask<MailLoginStep> BeginAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Each later step, with the client's response to the last challenge.
    /// </summary>
    /// <param name="response">The client's response, already decoded from base64.</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    /// <returns>The step: another challenge to send, or how the login ended.</returns>
    /// <exception cref="InvalidOperationException">
    /// The exchange has not begun, or its last step was not a <see cref="MailLoginOutcome.Challenge"/>.
    /// </exception>
    ValueTask<MailLoginStep> ContinueAsync(ReadOnlyMemory<byte> response, CancellationToken cancellationToken);
}
