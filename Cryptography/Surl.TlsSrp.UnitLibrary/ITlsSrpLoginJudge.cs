namespace Surl.TlsSrp;

/// <summary>
/// Lets the owner of a <see cref="TlsSrpServer"/> see each TLS-SRP login it checks and refuse chosen ones
/// (ADR-0165 decisions 2 to 4). It stays free of every Surl project: the engine adapts it.
/// </summary>
public interface ITlsSrpLoginJudge
{
    /// <summary>
    /// Asked once the client's <c>Finished</c> has verified, so only a login whose credentials matched is asked.
    /// </summary>
    /// <param name="userName">The <c>srp</c> user name.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns><see langword="true"/> to refuse the login as a wrong password is refused.</returns>
    ValueTask<bool> ShouldRefuseAsync(string userName, CancellationToken cancellationToken);

    /// <summary>Called once with the verdict of a login that reached the client's <c>Finished</c>.</summary>
    /// <param name="userName">The <c>srp</c> user name.</param>
    /// <param name="isAccepted">False for a wrong password, an unknown user or a refused login.</param>
    void Judged(string userName, bool isAccepted);
}
