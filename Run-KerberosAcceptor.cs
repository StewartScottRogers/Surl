#:project Surl.Kerberos.UnitLibrary/Surl.Kerberos.UnitLibrary.csproj

// Answers a pinned upstream curl build's Kerberos inside an LDAP GSS-SPNEGO bind for
// Record-CurlExchange.ps1 -LdapKerberosAcceptor (BL-327, ADR-0072 Amendment 1), with the keys
// -KerberosTestKdc wrote to service.keytab:
//
//   dotnet run Run-KerberosAcceptor.cs -- --keytab service.keytab [--service ldap]
//
// It prints "ready" on standard output, then answers one line per line read from standard
// input until it closes, every byte string in hex:
//
//   spnego <token>  -> ok <negTokenResp> <conf|integ> <client principal>, or refused <reason>,
//                      for a NegTokenInit whose optimistic token is a Kerberos AP-REQ: the
//                      negTokenResp is accept-completed, supportedMech the client's first OID,
//                      and the AP-REP when the client asked for mutual authentication
//   unwrap <token>  -> ok <message> sealed|signed, or refused
//   wrap <message>  -> ok <token>: sealed when the client asked for confidentiality
//
// It is a measuring fixture, never a surl server: surl answers these binds through
// Surl.Authentication's GSS-SPNEGO exchange.
using System.Formats.Asn1;
using System.Security.Cryptography;
using Surl.Kerberos;

string? keytabPath = args.Length >= 2 && args[0] == "--keytab" ? args[1] : null;
string service = args.Length >= 4 && args[2] == "--service" ? args[3] : "ldap";
KerberosKeytab? keytab = keytabPath is null ? null : KerberosKeytab.Read(await File.ReadAllBytesAsync(keytabPath)).Keytab;
if (keytab is null)
{
    await Console.Error.WriteLineAsync("Run-KerberosAcceptor.cs needs --keytab <an MIT keytab> [--service <service>].");
    return 2;
}

KerberosAcceptorFixture fixture = new(new KerberosAcceptor(keytab, new KerberosReplayCache(TimeProvider.System), TimeProvider.System, new CryptographicRandomSource()), service);
Console.WriteLine("ready");
Console.Out.Flush();
while (await Console.In.ReadLineAsync() is { } line)
{
    Console.WriteLine(fixture.Answer(line));
    Console.Out.Flush();
}

return 0;

/// <summary>One bind's acceptor: the context its NegTokenInit opened, then its wrap tokens.</summary>
internal sealed class KerberosAcceptorFixture(KerberosAcceptor acceptor, string service)
{
    private KerberosSecurityContext? context;

    public string Answer(string line)
    {
        string[] words = line.Split(' ', 2);
        byte[] bytes = words.Length > 1 ? Convert.FromHexString(words[1]) : [];
        return (words[0], context) switch
        {
            ("spnego", _) => AnswerNegTokenInit(bytes),
            ("unwrap", { } accepted) => Unwrap(accepted, bytes),
            ("wrap", { } accepted) => "ok " + Convert.ToHexString(accepted.IsConfidentialityRequested ? accepted.Seal(bytes) : accepted.Wrap(bytes)),
            _ => "refused no accepted bind",
        };
    }

    private static string Unwrap(KerberosSecurityContext accepted, byte[] token) =>
        accepted.TryUnwrap(token, out byte[] message)
            ? $"ok {Convert.ToHexString(message)} {((token[2] & 0x02) != 0 ? "sealed" : "signed")}"
            : "refused";

    private static Asn1Tag ContextTag(int number) => new(TagClass.ContextSpecific, number, isConstructed: true);

    // InitialContextToken [APPLICATION 0] { SPNEGO OID, [0] NegTokenInit { mechTypes [0],
    //   reqFlags [1] OPTIONAL, mechToken [2] OPTIONAL, mechListMIC [3] OPTIONAL } }
    private string AnswerNegTokenInit(byte[] token)
    {
        try
        {
            AsnReader initialContextToken = new AsnReader(token, AsnEncodingRules.DER).ReadSequence(new Asn1Tag(TagClass.Application, 0, isConstructed: true));
            initialContextToken.ReadObjectIdentifier();
            AsnReader fields = initialContextToken.ReadSequence(ContextTag(0)).ReadSequence();
            string firstMech = fields.ReadSequence(ContextTag(0)).ReadSequence().ReadObjectIdentifier();
            byte[]? mechToken = ReadMechToken(fields);
            return mechToken is null ? "refused no optimistic token" : Accept(mechToken, firstMech);
        }
        catch (AsnContentException)
        {
            return "refused malformed SPNEGO";
        }
    }

    private static byte[]? ReadMechToken(AsnReader fields)
    {
        while (fields.HasData)
        {
            Asn1Tag tag = fields.PeekTag();
            AsnReader field = fields.ReadSequence(tag);
            if (tag == ContextTag(2))
            {
                return field.ReadOctetString();
            }
        }

        return null;
    }

    private string Accept(byte[] apRequest, string firstMech)
    {
        KerberosAcceptResult result = acceptor.AcceptInsideSpnego(apRequest, service);
        if (result.Context is not { } accepted)
        {
            return "refused " + result.RefusalReason;
        }

        context = accepted;
        string layer = accepted.IsConfidentialityRequested ? "conf" : "integ";
        return $"ok {Convert.ToHexString(WriteNegTokenResp(accepted, firstMech))} {layer} {accepted.ClientPrincipal}";
    }

    // negTokenResp [1] { negState [0] accept-completed, supportedMech [1], responseToken [2] }
    private static byte[] WriteNegTokenResp(KerberosSecurityContext accepted, string supportedMech)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence(ContextTag(1)))
        using (writer.PushSequence())
        {
            using (writer.PushSequence(ContextTag(0)))
            {
                writer.WriteEnumeratedValue(NegState.AcceptCompleted);
            }

            using (writer.PushSequence(ContextTag(1)))
            {
                writer.WriteObjectIdentifier(supportedMech);
            }

            if (accepted.IsMutualAuthenticationRequested)
            {
                using (writer.PushSequence(ContextTag(2)))
                {
                    writer.WriteOctetString(accepted.CreateApRepToken());
                }
            }
        }

        return writer.Encode();
    }
}

/// <summary>The AP-REP's confounder and sequence number, from the operating system's generator.</summary>
internal sealed class CryptographicRandomSource : IKerberosRandomSource
{
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}

/// <summary>RFC 4178 section 4.2.2's <c>negState</c>, the one value this fixture sends.</summary>
internal enum NegState
{
    AcceptCompleted = 0,
}
