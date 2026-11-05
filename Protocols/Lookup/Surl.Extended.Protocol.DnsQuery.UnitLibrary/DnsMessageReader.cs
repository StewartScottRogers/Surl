using System.Net;
using System.Text;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// Reads a DNS message from its wire form (RFC 1035 section 4.1): the header, every question and
/// every record the counts announce, each RDATA field by field for the types ADR-0092 decision 2
/// serves and as opaque bytes for the rest (RFC 3597). It refuses, as a
/// <see cref="DnsReadFault"/>, anything it cannot read exactly: a short header, a read past the
/// end, a reserved label type, a name past 255 bytes, a compression pointer past the end or not
/// strictly backwards (which is what refuses a loop), RDATA that does not hold its type's fields,
/// and bytes after the last record. What a server answers to each is ADR-0092's, not the reader's.
/// </summary>
public static class DnsMessageReader
{
    private static readonly Dictionary<DnsRecordType, Func<DnsMessageCursor, DnsRecordData>> FieldReaders = new()
    {
        [DnsRecordType.A] = cursor => new DnsAddressData(new IPAddress(cursor.ReadBytes(4))),
        [DnsRecordType.NS] = cursor => new DnsDomainNameData(DnsRecordType.NS, cursor.ReadName()),
        [DnsRecordType.CNAME] = cursor => new DnsDomainNameData(DnsRecordType.CNAME, cursor.ReadName()),
        [DnsRecordType.SOA] = cursor => new DnsStartOfAuthorityData(
            cursor.ReadName(),
            cursor.ReadName(),
            cursor.ReadUInt32(),
            cursor.ReadUInt32(),
            cursor.ReadUInt32(),
            cursor.ReadUInt32(),
            cursor.ReadUInt32()),
        [DnsRecordType.PTR] = cursor => new DnsDomainNameData(DnsRecordType.PTR, cursor.ReadName()),
        [DnsRecordType.MX] = cursor => new DnsMailExchangeData(cursor.ReadUInt16(), cursor.ReadName()),
        [DnsRecordType.TXT] = ReadText,
        [DnsRecordType.AAAA] = cursor => new DnsAddressData(new IPAddress(cursor.ReadBytes(16))),
        [DnsRecordType.SRV] = cursor => new DnsServiceLocationData(
            cursor.ReadUInt16(), cursor.ReadUInt16(), cursor.ReadUInt16(), cursor.ReadName()),
        [DnsRecordType.OPT] = ReadOptions,
        [DnsRecordType.CAA] = ReadCertificationAuthorityAuthorization,
    };

    /// <summary>Reads <paramref name="message"/>, one whole DNS message.</summary>
    public static DnsReadOutcome Read(ReadOnlySpan<byte> message)
    {
        if (message.Length < DnsHeader.Length)
        {
            return new DnsReadOutcome(DnsReadFault.NoHeader, null, [], null);
        }

        var cursor = new DnsMessageCursor(message.ToArray());
        var header = DnsHeader.FromFlags(cursor.ReadUInt16(), cursor.ReadUInt16());
        var questions = new List<DnsQuestion>();

        try
        {
            var read = ReadSections(cursor, header, questions);
            return new DnsReadOutcome(DnsReadFault.None, header, questions, read);
        }
        catch (DnsReadFaultException exception)
        {
            return new DnsReadOutcome(exception.Fault, header, questions, null);
        }
    }

    private static DnsMessage ReadSections(DnsMessageCursor cursor, DnsHeader header, List<DnsQuestion> questions)
    {
        var questionCount = cursor.ReadUInt16();
        var answerCount = cursor.ReadUInt16();
        var authorityCount = cursor.ReadUInt16();
        var additionalCount = cursor.ReadUInt16();

        for (var index = 0; index < questionCount; index++)
        {
            questions.Add(new DnsQuestion(cursor.ReadName(), (DnsRecordType)cursor.ReadUInt16(), cursor.ReadUInt16()));
        }

        var answers = ReadRecords(cursor, answerCount);
        var authorities = ReadRecords(cursor, authorityCount);
        var additionals = ReadRecords(cursor, additionalCount);

        if (cursor.Position != cursor.Limit)
        {
            throw new DnsReadFaultException(DnsReadFault.BytesAfterLastRecord);
        }

        return new DnsMessage(header, questions, answers, authorities, additionals);
    }

    private static List<DnsResourceRecord> ReadRecords(DnsMessageCursor cursor, int count)
    {
        var records = new List<DnsResourceRecord>(count);

        for (var index = 0; index < count; index++)
        {
            var name = cursor.ReadName();
            var type = (DnsRecordType)cursor.ReadUInt16();
            var recordClass = cursor.ReadUInt16();
            var timeToLive = cursor.ReadUInt32();
            records.Add(new DnsResourceRecord(name, recordClass, timeToLive, ReadRecordData(cursor, type)));
        }

        return records;
    }

    private static DnsRecordData ReadRecordData(DnsMessageCursor cursor, DnsRecordType type)
    {
        cursor.EnterRecordData(cursor.ReadUInt16());
        var fieldReader = FieldReaders.GetValueOrDefault(type, ReadOpaque(type));

        DnsRecordData data;

        try
        {
            data = fieldReader(cursor);
        }
        catch (ArgumentException)
        {
            throw new DnsReadFaultException(DnsReadFault.RecordDataMalformed);
        }

        cursor.LeaveRecordData();
        return data;
    }

    private static Func<DnsMessageCursor, DnsRecordData> ReadOpaque(DnsRecordType type) =>
        cursor => new DnsOpaqueData(type, cursor.ReadBytes(cursor.Limit - cursor.Position));

    private static DnsTextData ReadText(DnsMessageCursor cursor)
    {
        var strings = new List<byte[]>();

        do
        {
            strings.Add(cursor.ReadBytes(cursor.ReadUInt8()));
        }
        while (cursor.Position < cursor.Limit);

        return new DnsTextData(strings);
    }

    private static DnsOptData ReadOptions(DnsMessageCursor cursor)
    {
        var options = new List<DnsOption>();

        while (cursor.Position < cursor.Limit)
        {
            var code = cursor.ReadUInt16();
            options.Add(new DnsOption(code, cursor.ReadBytes(cursor.ReadUInt16())));
        }

        return new DnsOptData(options);
    }

    private static DnsCertificationAuthorityAuthorizationData ReadCertificationAuthorityAuthorization(DnsMessageCursor cursor)
    {
        var flags = cursor.ReadUInt8();
        var tag = Encoding.ASCII.GetString(cursor.ReadBytes(cursor.ReadUInt8()));
        return new DnsCertificationAuthorityAuthorizationData(flags, tag, cursor.ReadBytes(cursor.Limit - cursor.Position));
    }
}
