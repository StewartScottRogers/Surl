namespace Surl.Protocol.Abstractions;

/// <summary>
/// Judges a TLS-SRP login inside the handshake, so the engine can ask the fault plan and publish
/// the <see cref="LoginJudgedEvent"/> although the handshake code holds no publisher (ADR-0165,
/// decision 2). A connection keeps the judge it is given through
/// <see cref="IConnection.UseTlsLoginJudge(ITlsLoginJudge)"/>.
/// </summary>
public interface ITlsLoginJudge
{
    /// <summary>
    /// Asks whether the login should be refused although its credentials matched. Called only once
    /// the client's <c>Finished</c> has verified (ADR-0165, decision 4).
    /// </summary>
    /// <param name="userName">The <c>srp</c> user name as the client sent it.</param>
    /// <param name="cancellationToken">Cuts the question off.</param>
    /// <returns><see langword="true"/> when the login must fail as a wrong password fails.</returns>
    ValueTask<bool> ShouldRefuseAsync(string userName, CancellationToken cancellationToken);

    /// <summary>
    /// Reports the final verdict of a login that reached its judgement.
    /// </summary>
    /// <param name="userName">The <c>srp</c> user name as the client sent it.</param>
    /// <param name="isAccepted">Whether the login was accepted.</param>
    void Judged(string userName, bool isAccepted);
}
