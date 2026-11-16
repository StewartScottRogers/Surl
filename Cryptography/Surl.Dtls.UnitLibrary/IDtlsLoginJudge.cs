namespace Surl.Dtls;

/// <summary>
/// Lets the owner of a <see cref="DtlsServerSession"/> see each PSK login it checks and refuse chosen ones
/// (ADR-0170 decisions 6 to 8, modelled on ADR-0165's TLS-SRP judge). It stays free of every Surl project.
/// </summary>
public interface IDtlsLoginJudge
{
    /// <summary>
    /// Asked once the client's <c>Finished</c> has verified, so only a login whose key matched is asked.
    /// </summary>
    /// <param name="identity">The PSK identity, read as UTF-8.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns><see langword="true"/> to refuse the login as a wrong key is refused (<c>decrypt_error</c>).</returns>
    ValueTask<bool> ShouldRefuseAsync(string identity, CancellationToken cancellationToken);

    /// <summary>Called once with the verdict of a login that reached the client's <c>Finished</c>.</summary>
    /// <param name="identity">The PSK identity, read as UTF-8.</param>
    /// <param name="isAccepted">False for a wrong key, an unknown identity or a refused login.</param>
    void Judged(string identity, bool isAccepted);
}
