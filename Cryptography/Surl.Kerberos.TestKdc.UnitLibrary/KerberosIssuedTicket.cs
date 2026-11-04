namespace Surl.Kerberos.TestKdc;

/// <summary>
/// What the test KDC puts into a ticket it issues, and repeats in the reply's encrypted part so
/// the client knows it: the flags, the session key, the client, the server and the times.
/// </summary>
/// <param name="Flags">The <c>TicketFlags</c>, bit 0 as <c>0x80000000</c>.</param>
/// <param name="SessionKeyType">The session key's enctype.</param>
/// <param name="SessionKey">The session key.</param>
/// <param name="Client">The client the ticket is for.</param>
/// <param name="Server">The server the ticket is to.</param>
/// <param name="AuthTime">When the client authenticated, in whole seconds.</param>
/// <param name="StartTime">When the ticket becomes valid, in whole seconds.</param>
/// <param name="EndTime">When the ticket expires, in whole seconds.</param>
internal sealed record KerberosIssuedTicket(
    uint Flags,
    KerberosEncryptionType SessionKeyType,
    byte[] SessionKey,
    KerberosPrincipalName Client,
    KerberosPrincipalName Server,
    DateTimeOffset AuthTime,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime);
